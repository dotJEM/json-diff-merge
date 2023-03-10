using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.DiffMerge.Test;

[TestFixture]
// ReSharper disable once InconsistentNaming
public class JTokenMergeVisitorTest_SimpleNonConflictedMerges : AbstractJTokenMergeVisitorTest
{
    [TestCaseSource(typeof(JTokenMergeVisitorTestData), nameof(JTokenMergeVisitorTestData.SimpleNonConflictedMerge))]
    public void Merge_WithoutConflicts_AllowsUpdate(JToken update, JToken conflict, JToken origin, JToken expected)
    {
        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);

        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).False);
        //Assert.That(result,
        //    ObjectHas.Property<MergeResult>(x => x.HasConflicts).False
        //    & ObjectHas.Property<MergeResult>(x => x.Merged).EqualTo(expected));
    }
}