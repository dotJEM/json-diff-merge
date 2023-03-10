using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.DiffMerge.Test;

public abstract class AbstractJTokenMergeVisitorTest
{
    public static JToken Json(object json)
    {
        if (json is string jsonStr)
            return JToken.Parse(jsonStr);

        if (json is JToken jsonToken)
            return jsonToken;

        return JToken.FromObject(json);
    }

    //public object[] Case(params string[] args)
    //{
    //    return args.Select(Json).Cast<object>().ToArray();
    //}

    public static TestCaseData Case(params string[] args)
    {
        return new TestCaseData(args.Select(Json).Cast<object>().ToArray());
    }
}