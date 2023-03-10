using System.Collections;

namespace DotJEM.Json.DiffMerge.Test;

public class JTokenMergeVisitorTestData : AbstractJTokenMergeVisitorTest
{
    public static IEnumerable SimpleNonConflictedMerge
    {
        get
        {
            yield return Case(
                "{ prop: 'what' }",
                "{ prop: 'what' }",
                "{ prop: 'what' }",
                "{ prop: 'what' }"
            );

            yield return Case(
                "{ prop: 'what' }",
                "{ prop: 'x' }",
                "{ prop: 'what' }",
                "{ prop: 'x' }"
            );

            yield return Case(
                "{ prop: { a: 42 } }",
                "{ prop: { a: 42, b: 'foo' } }",
                "{ prop: { a: 42 } }",
                "{ prop: { a: 42, b: 'foo' } }"
            );

            yield return Case(
                "{ prop: { a: 42 } }",
                "{ prop: { a: 42, b: 'foo' } }",
                "{ prop: { a: 42, b: 'foo' } }",
                "{ prop: { a: 42 } }"
            );

            yield return Case(
                "{ prop: { a: 42, b: 'foo' } }",
                "{ prop: { a: 42 } }",
                "{ prop: { a: 42, b: 'foo' } }",
                "{ prop: { a: 42 } }"
            );
        }
    }
}