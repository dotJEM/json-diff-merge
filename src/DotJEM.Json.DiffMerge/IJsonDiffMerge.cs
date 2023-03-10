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
        public IDiffMergeResult Diff(JToken left, JToken right, JToken origin = null)
        {
            return Diff(left, right, new JsonDiffMergeContext(left, right, origin));
        }

        public IDiffMergeResult Diff(JToken left, JToken right, IJsonDiffMergeContext context)
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

        public IDiffMergeResult Diff(JValue left, JValue right, IJsonDiffMergeContext context)
        {
            return !JToken.DeepEquals(left, right)
                ? context.Differs(left, right)
                : context.Equals(left, right);
        }

        public IDiffMergeResult Diff(JObject left, JObject right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from key in Keys(left, right)
                let diff = Diff(left[key], right[key], context.Next(key))
                select diff;

            return context.AddChildren(results, left, right);
        }

        public IDiffMergeResult Diff(JArray left, JArray right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from index in Enumerable.Range(0, Math.Max(left.Count, right.Count))
                let diff = Diff(left.Get(index) , right.Get(index), context.Next(index))
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
    }

    public class JsonDiffMergeContext : IJsonDiffMergeContext
    {
        private readonly JToken left;
        private readonly JToken right;
        private readonly JToken origin;
        private readonly List<IJsonDiffMergeContext> children = new List<IJsonDiffMergeContext>();

        public JsonDiffMergeContext(JToken left, JToken right, JToken origin)
        {
            this.left = left;
            this.right = right;
            this.origin = origin;
        }

        public IJsonDiffMergeContext Next(string key)
            => AddChildContext(new KeyJsonDiffMergeContext(key, left[key], right[key], origin?[key]));

        public IJsonDiffMergeContext Next(int index) 
            => AddChildContext(new IndexJsonDiffMergeContext(index, left[index], right[index], origin?[index]));

        public IDiffMergeResult Equals(JToken left, JToken right)
        {
            if (origin != null)
            {
                bool leftEqualsOrigin = JToken.DeepEquals(left, origin);
                bool rightEqualsOrigin =  JToken.DeepEquals(right, origin);
            }


            throw new NotImplementedException();
        }

        public IDiffMergeResult Differs(JToken left, JToken right)
        {
            if (origin != null)
            {
                //both changed and both are not equal to origin, that means we have a conflict.
                bool leftHasChanges = JToken.DeepEquals(left, origin);
                bool rightHasChanges =  JToken.DeepEquals(right, origin);
                bool hasConflicts = leftHasChanges && rightHasChanges;
                return new DiffMergeResult(hasConflicts, true, left, right, origin);
            }
            //TODO: We need to differ between 3way diff/merge and 2way diff/merge from the top.
            return new DiffMergeResult(false, true, left, right, origin);
        }

        public IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JObject left, JObject right)
        {
            throw new NotImplementedException();
        }

        public IDiffMergeResult AddChildren(IEnumerable<IDiffMergeResult> results, JArray left, JArray right)
        {
            throw new NotImplementedException();
        }

        private IJsonDiffMergeContext AddChildContext(IJsonDiffMergeContext child)
        {
            children.Add(child);
            return child;
        }
    }
    

    public interface IDiffMergeResult
    {
        bool HasConflicts { get; }
        bool HasDifferences { get; }

        JToken Left { get; }
        JToken Right { get; }
        JToken Origin { get; }

        JObject MergeLeftToRight();
        JObject MergeRightToLeft();


    }

    public class DiffMergeResult : IDiffMergeResult
    {
        public bool HasConflicts { get; }
        public bool HasDifferences { get; }

        public JToken Left { get; }
        public JToken Right { get; }
        public JToken Origin { get; }

        public DiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin)
        {
            HasConflicts = hasConflicts;
            HasDifferences = hasDifferences;

            Left = left;
            Right = right;
            Origin = origin;
        }

        public JObject MergeLeftToRight()
        {
            throw new NotImplementedException();
        }

        public JObject MergeRightToLeft()
        {
            throw new NotImplementedException();
        }
    }

    public class JObjectDiffMergeResult : DiffMergeResult
    {
        private readonly List<IDiffMergeResult> children;

        public JObjectDiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffMergeResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class JArrayDiffMergeResult : DiffMergeResult
    {
        private readonly List<IDiffMergeResult> children;

        public JArrayDiffMergeResult(bool hasConflicts, bool hasDifferences, JToken left, JToken right, JToken origin, IEnumerable<IDiffMergeResult> children) 
            : base(hasConflicts, hasDifferences, left, right, origin)
        {
            this.children = children.ToList();
        }
    }

    public class KeyJsonDiffMergeContext : JsonDiffMergeContext
    {
        private readonly string key;

        public KeyJsonDiffMergeContext(string key, JToken left, JToken right, JToken origin)
            : base(left, right, origin)
        {
            this.key = key;
        }
    }

    public class IndexJsonDiffMergeContext : JsonDiffMergeContext
    {
        private readonly int index;

        public IndexJsonDiffMergeContext(int index, JToken left, JToken right, JToken origin)
            : base(left, right, origin)
        {
            this.index = index;
        }
    }
}
