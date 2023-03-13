using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.DiffMerge;

public static class ThreeWayJsonDiffExtensions
{
  
    public static IDiffCompareResult Diff(this IJsonDiffComparer self, JToken left, JToken right, JToken origin, I3WayJsonDiffCompareOptions options = null)
    {
         options ??= new ThreeWayJsonDiffCompareOptions();
        IJsonDiffCompareContext context = new JsonThreeWayDiffCompareContext(left, right, origin, options);
        return self.Diff(left, right, context);
    }
}
public interface IJsonDiffComparer
{
    IDiffCompareResult Diff(JToken left, JToken right, IJsonDiffCompareContext context);
}

public class JsonDiffComparer : IJsonDiffComparer
{
    public IDiffCompareResult Diff(JToken left, JToken right, IJsonDiffCompareContext context)
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
        return context.ArrayHandler.Diff(left, right, this);
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
    IDiffCompareArrayHandling ArrayHandler { get; }
    IJsonDiffCompareContext Next(string key);
    //IJsonDiffCompareContext Next(int index);
    IDiffCompareResult Equals(JToken left, JToken right);
    IDiffCompareResult Differs(JToken left, JToken right);

    IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JObject left, JObject right);
    IDiffCompareResult AddChildren(IEnumerable<IDiffCompareResult> results, JArray left, JArray right);
}


public class JsonThreeWayDiffCompareContext : IJsonDiffCompareContext
{
    private readonly I3WayJsonDiffCompareOptions options;

    private readonly JToken left;
    private readonly JToken right;
    private readonly JToken origin;

    private readonly List<IJsonDiffCompareContext> children = new List<IJsonDiffCompareContext>();

    public IDiffCompareArrayHandling ArrayHandler { get; }

    public JsonThreeWayDiffCompareContext(JToken left, JToken right, JToken origin, I3WayJsonDiffCompareOptions options)
    {
        this.left = left;
        this.right = right;
        this.origin = origin;
        this.options = options;
        this.ArrayHandler = options.ArrayHandlerFactory(origin, this);
    }

    public IJsonDiffCompareContext Next(string key)
        => AddChildContext(new KeyJsonThreeWayDiffCompareContext(key, left[key], right[key], origin?[key],options));

    //public IJsonDiffCompareContext Next(int index)
    //    => AddChildContext(new IndexJsonThreeWayDiffCompareContext(index, index, index, left[index], right[index], origin?[index], alignment, options));

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


    private IJsonDiffCompareContext AddChildContext(IJsonDiffCompareContext child)
    {
        children.Add(child);
        return child;
    }
}

public interface IDiffCompareArrayHandling
{
    IDiffCompareResult Diff(JArray left, JArray right, IJsonDiffComparer comparer);
}

public class AsValueThreeWayArrayHandling : IDiffCompareArrayHandling
{
    private readonly JToken origin;
    private readonly IJsonDiffCompareContext context;

    public AsValueThreeWayArrayHandling(JToken origin, IJsonDiffCompareContext context)
    {
        this.origin = origin;
        this.context = context;
    }

    public IDiffCompareResult Diff(JArray left, JArray right, IJsonDiffComparer comparer)
    {
        bool areEquals = JToken.DeepEquals(left, right);
        bool leftHasChanges = !JToken.DeepEquals(left, origin);
        bool rightHasChanges = !JToken.DeepEquals(right, origin);
        return new ThreeWayDiffCompareResult(
            !areEquals && leftHasChanges && rightHasChanges,
            //note: 
            leftHasChanges || rightHasChanges,
            left, right, origin
        );
    }
}

public struct ThreeWayAligned
{
    public ValueWithIndex Left { get; }
    public ValueWithIndex Right { get; }
    public ValueWithIndex Origin { get; }

    public ThreeWayAligned(ValueWithIndex left, ValueWithIndex right, ValueWithIndex origin)
    {
        Left = left;
        Right = right;
        Origin = origin;
    }
}
public struct ValueWithIndex
{
    public int Index { get; }
    public JToken Value { get; }

    public ValueWithIndex(int index, JToken value)
    {
        Index = index;
        Value = value;
    }

    public static implicit operator JToken(ValueWithIndex val) => val.Value;
}


public interface I3WayJsonDiffCompareOptions
{
    Func<JToken, IJsonDiffCompareContext, IDiffCompareArrayHandling> ArrayHandlerFactory { get; }
}

public class ThreeWayJsonDiffCompareOptions : I3WayJsonDiffCompareOptions
{
    public Func<JToken, IJsonDiffCompareContext, IDiffCompareArrayHandling> ArrayHandlerFactory { get; set; }
        = (token, context) => new AsValueThreeWayArrayHandling(token, context);
}


public interface IDiffCompareResult
{
    bool HasConflicts { get; }
    bool HasDifferences { get; }

    JToken Left { get; }
    JToken Right { get; }
    //JToken Origin { get; }

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

    public KeyJsonThreeWayDiffCompareContext(string key, JToken left, JToken right, JToken origin, I3WayJsonDiffCompareOptions options)
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

    public IndexJsonThreeWayDiffCompareContext(int leftIndex, int rightIndex, int originIndex, JToken left, JToken right, JToken origin, I3WayJsonDiffCompareOptions options)
        : base(left, right, origin,  options)
    {
        this.leftIndex = leftIndex;
        this.rightIndex = rightIndex;
        this.originIndex = originIndex;
    }
}
