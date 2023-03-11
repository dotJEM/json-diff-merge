using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.DiffMerge
{

    public interface IJsonDiffComparer
    {
        IDiffCompareResult Diff(JToken left, JToken right, JToken origin = null);
    }

    public class JsonDiffComparer : IJsonDiffComparer
    {
        private readonly IJsonDiffCompareOptions options;

        public JsonDiffComparer(IJsonDiffCompareOptions options = null)
        {
            this.options = options ?? new JsonDiffCompareOptions();
        }

        public IDiffCompareResult Diff(JToken left, JToken right, JToken origin = null)
        {
            return Diff(left, right, new JsonThreeWayDiffCompareContext(left, right, origin, options));
        }

        private IDiffCompareResult Diff(JToken left, JToken right, IJsonDiffCompareContext context)
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

        private IDiffCompareResult Diff(JValue left, JValue right, IJsonDiffCompareContext context)
        {
            return !JToken.DeepEquals(left, right)
                ? context.Differs(left, right)
                : context.Equals(left, right);
        }

        private IDiffCompareResult Diff(JObject left, JObject right, IJsonDiffCompareContext context)
        {
            IEnumerable<IDiffCompareResult> results = from key in Keys(left, right)
                let diff = Diff(left[key], right[key], context.Next(key))
                select diff;

            return context.AddChildren(results, left, right);
        }

        private IDiffCompareResult Diff(JArray left, JArray right, IJsonDiffCompareContext context)
        {
            IEnumerable<IDiffCompareResult> results = from aligned in context.Align(left, right)
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

    public interface IJsonDiffCompareContext
    {
        IJsonDiffCompareContext Next(string key);
        IJsonDiffCompareContext Next(int index);
        IDiffCompareResult Equals(JToken left, JToken right);
        IDiffCompareResult Differs(JToken left, JToken right);

        IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JObject left, JObject right);
        IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JArray left, JArray right);
        IEnumerable<AlignedItem> Align(JArray left, JArray right);
    }

    public class AlignedItem
    {
        public JToken Left { get; }
        public JToken Right { get; }
        public IJsonDiffCompareContext Context { get; }

        public AlignedItem(JToken left, JToken right, IJsonDiffCompareContext context)
        {
            Left = left;
            Right = right;
            Context = context;
        }
    }

    public class JsonThreeWayDiffCompareContext : IJsonDiffCompareContext
    {
        private readonly IJsonDiffCompareOptions options;

        private readonly JToken left;
        private readonly JToken right;
        private readonly JToken origin;

        private readonly List<IJsonDiffCompareContext> children = new List<IJsonDiffCompareContext>();

        public JsonThreeWayDiffCompareContext(JToken left, JToken right, JToken origin, IJsonDiffCompareOptions options)
        {
            this.left = left;
            this.right = right;
            this.origin = origin;
            this.options = options;
        }

        public IJsonDiffCompareContext Next(string key)
            => AddChildContext(new KeyJsonThreeWayDiffCompareContext(key, left[key], right[key], origin?[key], options));

        public IJsonDiffCompareContext Next(int index) 
            => AddChildContext(new IndexJsonThreeWayDiffCompareContext(index, index, index, left[index], right[index], origin?[index], options));

        public IDiffCompareResult Equals(JToken left, JToken right)
        {
            bool hasChanges = !JToken.DeepEquals(left, origin);
            return new ThreeWayDiffCompareResult(false, hasChanges, left, right, origin);
        }

        public IDiffCompareResult Differs(JToken left, JToken right)
        {
            bool leftHasChanges = !JToken.DeepEquals(left, origin);
            bool rightHasChanges = !JToken.DeepEquals(right, origin);
            //both changed and both are not equal to origin, that means we have a conflict.
            bool hasConflicts = leftHasChanges && rightHasChanges;
            return new ThreeWayDiffCompareResult(hasConflicts, true, left, right, origin);
        }

        public IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JObject left, JObject right)
        {
            results = results.ToList();
            bool hasConflicts = results.Any(x => x.HasConflicts);
            bool hasDifferences = results.Any(x => x.HasDifferences);
            return new JObjectThreeWayDiffCompareResult(hasConflicts, hasDifferences, left, right, origin, results);
        }

        public IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JArray left, JArray right)
        {
            results = results.ToList();
            bool hasConflicts = results.Any(x => x.HasConflicts);
            bool hasDifferences = results.Any(x => x.HasDifferences);
            return new JArrayThreeWayDiffCompareResult(hasConflicts, hasDifferences, left, right, origin, results);
        }

        public IEnumerable<AlignedItem> Align(JArray left, JArray right)
        {
            int count = Math.Max(left.Count, right.Count);
            if (this.origin is JArray originArr)
            {
                count = Math.Max(originArr.Count, count);
                for (int i = 0; i < count; i++)
                {
                    yield return new AlignedItem(left.Get(i), right.Get(i), new IndexJsonThreeWayDiffCompareContext(i, i, i, left.Get(i), right.Get(i), originArr.Get(i), options));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    yield return new AlignedItem(left.Get(i), right.Get(i), new IndexJsonThreeWayDiffCompareContext(i, i, -1, left.Get(i), right.Get(i), null, options));
                }
            }
        }

        private IJsonDiffCompareContext AddChildContext(IJsonDiffCompareContext child)
        {
            children.Add(child);
            return child;
        }


    }

    public interface IArrayAlignment
    {

    }

    public interface IJsonDiffCompareOptions
    {
        IArrayAlignment ArrayAlignment { get; }
    }

    public class JsonDiffCompareOptions : IJsonDiffCompareOptions
    {
        public IArrayAlignment ArrayAlignment { get; }
    }


    public interface IDiffCompareResult
    {
        bool HasConflicts { get; }
        bool HasDifferences { get; }

        JToken Left { get; }
        JToken Right { get; }
        JToken Origin { get; }

        bool TryMerge(out JToken merged);
    }

    public class ThreeWayDiffCompareResult : IDiffCompareResult
    {
        public bool HasConflicts { get; }
        public bool HasDifferences { get; }

        public JToken Left { get; }
        public JToken Right { get; }
        public JToken Origin { get; }

        public ThreeWayDiffCompareResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin)
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

    public class JObjectThreeWayDiffCompareResult : ThreeWayDiffCompareResult
    {
        private readonly List<IDiffCompareResult> children;

        public JObjectThreeWayDiffCompareResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffCompareResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class JArrayThreeWayDiffCompareResult : ThreeWayDiffCompareResult
    {
        private readonly List<IDiffCompareResult> children;

        public JArrayThreeWayDiffCompareResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffCompareResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class KeyJsonThreeWayDiffCompareContext : JsonThreeWayDiffCompareContext
    {
        private readonly string key;

        public KeyJsonThreeWayDiffCompareContext(string key, JToken left, JToken right, JToken origin, IJsonDiffCompareOptions options)
            : base(left, right, origin, options)
        {
            this.key = key;
        }
    }

    public class IndexJsonThreeWayDiffCompareContext : JsonThreeWayDiffCompareContext
    {
        private readonly int leftIndex;
        private readonly int rightIndex;
        private readonly int originIndex;

        public IndexJsonThreeWayDiffCompareContext(int leftIndex, int rightIndex, int originIndex, JToken left, JToken right, JToken origin, IJsonDiffCompareOptions options)
            : base(left, right, origin, options)
        {
            this.leftIndex = leftIndex;
            this.rightIndex = rightIndex;
            this.originIndex = originIndex;
        }
    }
}
