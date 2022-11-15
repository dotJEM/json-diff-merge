using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.DiffMerge
{

    public interface IDiffMergeResult
    {
        bool HasConflicts { get; }

        JObject MergeLeftToRight();
        JObject MergeRightToLeft();


    }

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
                return context.Noop(null, null);

            if (left == null || right == null || left.Type != right.Type)
                return context.Record(left, right);

            if (left is JValue)
                return Diff(left, right as JValue, context);

            if (left is JArray)
                return Diff(left, right as JArray, context);

            if (left is JObject)
                return Diff(left, right as JObject, context);

            throw new ArgumentOutOfRangeException();
        }

        public IDiffMergeResult Diff(JValue left, JValue right, IJsonDiffMergeContext context)
        {
            return !JToken.DeepEquals(left, right)
                ? context.Record(left, right)
                : context.Noop(left, right);
        }

        public IDiffMergeResult Diff(JObject left, JObject right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from key in Keys(left, right)
                let diff = Diff(left[key], right[key], context.Next(key))
                select diff;

            return context.Records(results, left, right);
        }

        public IDiffMergeResult Diff(JArray left, JArray right, IJsonDiffMergeContext context)
        {
            IEnumerable<IDiffMergeResult> results = from index in Enumerable.Range(0, Math.Max(left.Count, right.Count))
                let diff = Diff(left[index], right[index], context.Next(index))
                select diff;
            return context.Records(results, left, right);
        }

        private IEnumerable<string> Keys(IDictionary<string, JToken> left, IDictionary<string, JToken> right)
        {
            return left.Keys.Union(right.Keys).Distinct();
        }
    }

    public interface IJsonDiffMergeContext
    {
        IJsonDiffMergeContext Next(string key);
        IJsonDiffMergeContext Next(int index);
        IDiffMergeResult Noop(JToken left, JToken right);
        IDiffMergeResult Record(JToken left, JToken right);
        IDiffMergeResult Records(IEnumerable<IDiffMergeResult> results, JObject left, JObject right);
        IDiffMergeResult Records(IEnumerable<IDiffMergeResult> results, JArray left, JArray right);
    }

    public class JsonDiffMergeContext : IJsonDiffMergeContext
    {
        private readonly JToken left;
        private readonly JToken right;
        private readonly JToken origin;

        public JsonDiffMergeContext(JToken left, JToken right, JToken origin)
        {
            this.left = left;
            this.right = right;
            this.origin = origin;
        }

        public IJsonDiffMergeContext Next(string key)
        {
            return new KeyJsonDiffMergeContext(key, left[key], right[key], origin?[key]);
        }

        public IJsonDiffMergeContext Next(int index)
        {
            return new IndexJsonDiffMergeContext(index, left[index], right[index], origin?[index]);
        }

        public IDiffMergeResult Noop(JToken left, JToken right)
        {
            throw new NotImplementedException();
        }

        public IDiffMergeResult Record(JToken left, JToken right)
        {
            throw new NotImplementedException();
        }

        public IDiffMergeResult Records(IEnumerable<IDiffMergeResult> results, JObject left, JObject right)
        {
            throw new NotImplementedException();
        }

        public IDiffMergeResult Records(IEnumerable<IDiffMergeResult> results, JArray left, JArray right)
        {
            throw new NotImplementedException();
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
