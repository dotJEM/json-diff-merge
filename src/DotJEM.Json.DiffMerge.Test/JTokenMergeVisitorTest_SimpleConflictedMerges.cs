using System.Collections;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.DiffMerge.Test;

[TestFixture]
// ReSharper disable once InconsistentNaming
public class JTokenMergeVisitorTest_SimpleConflictedMerges : AbstractJTokenMergeVisitorTest
{
    [TestCaseSource(nameof(ConflictedMerges))]
    public void Merge_WithConflicts_DisallowsUpdate(JToken update, JToken conflict, JToken origin, JToken expected)
    {
        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);

        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).True);
        //& Has.Property<MergeResult>(nameof(IDiffMergeResult))).EqualTo(expected));
    }

    public static IEnumerable ConflictedMerges
    {
        get
        {
            yield return Case(
                "{ prop: 'hey' }",
                "{ prop: 'ho' }",
                "{ prop: 'what' }",
                "{ prop: { origin: 'what', update: 'hey', other: 'ho' } }"
            );

            yield return Case(
                "{ prop: { a: 42, b: 'hey' } }",
                "{ prop: { a: 42, b: 'ho' } }",
                "{ prop: { a: 42, b: 'what' } }",
                "{ 'prop.b': { origin: 'what', update: 'hey', other: 'ho' } }"
            );
        }
    }
}