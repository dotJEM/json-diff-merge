using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.DiffMerge
{

    public interface IJsonDiffMerge
    {
        IDiffMergeResult Diff(JToken left, JToken right, JToken origin = null);
    }

    public class JsonDiffMerge : IJsonDiffMerge
    {
        private readonly IJsonDiffMergeOptions options;

        public JsonDiffMerge(IJsonDiffMergeOptions options = null)
        {
            this.options = options ?? new JsonDiffMergeOptions();
        }

        public IDiffMergeResult Diff(JToken left, JToken right, JToken origin = null)
        {
            return Diff(left, right, new JsonThreeWayDiffMergeContext(left, right, origin, options));
        }

        private IDiffMergeResult Diff(JToken left, JToken right, IJsonDiffMergeContext context)
        {
            if (left == null && right == null)
                return context.Equals(null, null);

            if (left == null || right == null || left.Type != right.Type)
                return context.Differs(left, right);

            if (left is JValue leftValue)
                return Diff(leftValue, right as JValue, context);

            if (left is JArray leftArray)
                return Diff(leftArray, right as JArray, context);
            
            if (left is JObject leftObject)
                return Diff(leftObject, right as JObject, context);
            
            throw new ArgumentOutOfRangeException();
        }

        private IDiffMergeResult Diff(JValue left, JValue right, IJsonDiffMergeContext context)
        {
            return !JToken.DeepEquals(left, right)
                ? context.Differs(left, right)
                : context.Equals(left, right);
        }

        private IDiffMergeResult Diff(JObject left, JObject right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from key in Keys(left, right)
                let diff = Diff(left[key], right[key], context.Next(key))
                select diff;

            return context.AddChildren(results, left, right);
        }

        private IDiffMergeResult Diff(JArray left, JArray right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from aligned in context.Align(left, right)
                let diff = Diff(aligned.Left, aligned.Right, aligned.Context)
                select diff;
            return context.AddChildren(results, left, right);
        }

        private IEnumerable<string> Keys(IDictionary<string, JToken> left, IDictionary<string, JToken> right)
        {
            return left.Keys.Union(right.Keys).Distinct();
        }

    }

    internal static class JsonExtensions
    {
        public static JToken Get(this JArray self, int index)
        {
            return self.Count > index ? self[index] : null;
        }
    }

    public interface IJsonDiffMergeContext
    {
        IJsonDiffMergeContext Next(string key);
        IJsonDiffMergeContext Next(int index);
        IDiffMergeResult Equals(JToken left, JToken right);
        IDiffMergeResult Differs(JToken left, JToken right);

        IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JObject left, JObject right);
        IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JArray left, JArray right);
        IEnumerable<AlignedItem> Align(JArray left, JArray right);
    }

    public class AlignedItem
    {
        public JToken Left { get; }
        public JToken Right { get; }
        public IJsonDiffMergeContext Context { get; }

        public AlignedItem(JToken left, JToken right, IJsonDiffMergeContext context)
        {
            Left = left;
            Right = right;
            Context = context;
        }
    }

    public class JsonThreeWayDiffMergeContext : IJsonDiffMergeContext
    {
        private readonly IJsonDiffMergeOptions options;

        private readonly JToken left;
        private readonly JToken right;
        private readonly JToken origin;

        private readonly List<IJsonDiffMergeContext> children = new List<IJsonDiffMergeContext>();

        public JsonThreeWayDiffMergeContext(JToken left, JToken right, JToken origin, IJsonDiffMergeOptions options)
        {
            this.left = left;
            this.right = right;
            this.origin = origin;
            this.options = options;
        }

        public IJsonDiffMergeContext Next(string key)
            => AddChildContext(new KeyJsonThreeWayDiffMergeContext(key, left[key], right[key], origin?[key], options));

        public IJsonDiffMergeContext Next(int index) 
            => AddChildContext(new IndexJsonThreeWayDiffMergeContext(index, index, index, left[index], right[index], origin?[index], options));

        public IDiffMergeResult Equals(JToken left, JToken right)
        {
            bool leftEqualsOrigin = JToken.DeepEquals(left, origin);
            bool rightEqualsOrigin =  JToken.DeepEquals(right, origin);
            bool hasChanges = leftEqualsOrigin || rightEqualsOrigin;
            return new ThreeWayDiffMergeResult(false, hasChanges, left, right, origin);
        }

        public IDiffMergeResult Differs(JToken left, JToken right)
        {
            bool leftHasChanges = JToken.DeepEquals(left, origin);
            bool rightHasChanges =  JToken.DeepEquals(right, origin);
            //both changed and both are not equal to origin, that means we have a conflict.
            bool hasConflicts = leftHasChanges && rightHasChanges;
            return new ThreeWayDiffMergeResult(hasConflicts, true, left, right, origin);
        }

        public IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JObject left, JObject right)
        {
            //bool areAnyDifferent = results.Any()
            throw new NotImplementedException();
        }

        public IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JArray left, JArray right)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AlignedItem> Align(JArray left, JArray right)
        {
            JArray origin = this.origin as JArray;
            int count = Math.Max(left.Count, right.Count);
            if (origin != null)
            {
                count = Math.Max(origin.Count, count);
                for (int i = 0; i < count; i++)
                {
                    yield return new AlignedItem(left.Get(i), right.Get(i), new IndexJsonThreeWayDiffMergeContext(i, i, i, left.Get(i), right.Get(i), origin.Get(i), options));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new AlignedItem(left.Get(i), right.Get(i), new IndexJsonThreeWayDiffMergeContext(i, i, -1, left.Get(i), right.Get(i), null, options));
                }
            }
        }

        private IJsonDiffMergeContext AddChildContext(IJsonDiffMergeContext child)
        {
            children.Add(child);
            return child;
        }


    }

    public interface IArrayAlignment
    {

    }

    public interface IJsonDiffMergeOptions
    {
        IArrayAlignment ArrayAlignment { get; }
    }

    public class JsonDiffMergeOptions : IJsonDiffMergeOptions
    {
        public IArrayAlignment ArrayAlignment { get; }
    }


    public interface IDiffMergeResult
    {
        bool HasConflicts { get; }
        bool HasDifferences { get; }

        JToken Left { get; }
        JToken Right { get; }
        JToken Origin { get; }

        bool TryMerge(out JToken merged);
    }

    public class ThreeWayDiffMergeResult : IDiffMergeResult
    {
        public bool HasConflicts { get; }
        public bool HasDifferences { get; }

        public JToken Left { get; }
        public JToken Right { get; }
        public JToken Origin { get; }

        public ThreeWayDiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin)
        {
            HasConflicts = hasConflicts;
            HasDifferences = hasDifferences;

            Left = left;
            Right = right;
            Origin = origin;
        }
        public bool TryMerge(out JToken merged)
        {
            merged = null;
            if (HasConflicts)
                return false;

            throw new NotImplementedException();
        }

    }

    public class JObjectThreeWayDiffMergeResult : ThreeWayDiffMergeResult
    {
        private readonly List<IDiffMergeResult> children;

        public JObjectThreeWayDiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffMergeResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class JArrayThreeWayDiffMergeResult : ThreeWayDiffMergeResult
    {
        private readonly List<IDiffMergeResult> children;

        public JArrayThreeWayDiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffMergeResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class KeyJsonThreeWayDiffMergeContext : JsonThreeWayDiffMergeContext
    {
        private readonly string key;

        public KeyJsonThreeWayDiffMergeContext(string key, JToken left, JToken right, JToken origin, IJsonDiffMergeOptions options)
            : base(left, right, origin, options)
        {
            this.key = key;
        }
    }

    public class IndexJsonThreeWayDiffMergeContext : JsonThreeWayDiffMergeContext
    {
        private readonly int leftIndex;
        private readonly int rightIndex;
        private readonly int originIndex;

        public IndexJsonThreeWayDiffMergeContext(int leftIndex, int rightIndex, int originIndex, JToken left, JToken right, JToken origin, IJsonDiffMergeOptions options)
            : base(left, right, origin, options)
        {
            this.leftIndex = leftIndex;
            this.rightIndex = rightIndex;
            this.originIndex = originIndex;
        }
    }
}
