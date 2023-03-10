using System.Collections;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.DiffMerge.Test;

[TestFixture]
// ReSharper disable once InconsistentNaming
public class JTokenMergeVisitorTest_SimpleArrayHandling : AbstractJTokenMergeVisitorTest
{
    [Test]
    public void Merge_ScrambledIntArray_IsConflicted()
    {
        JToken update = Json("{ arr: [1,2,3] }");
        JToken conflict = Json("{ arr: [2,3,1] }");
        JToken origin = Json("{ arr: [3,1,2] }");
        JToken diff = Json("{ arr: { origin: [3,1,2], other: [2,3,1], update: [1,2,3] } }");

        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);

        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).True);
        //Assert.That(result, ObjectHas.Property<MergeResult>(x => x.HasConflicts).True
        //                    & ObjectHas.Property<MergeResult>(x => x.Conflicts).EqualTo(diff));
    }
    [Test]
    public void Merge_TypeMismatchArray_IsConflicted()
    {
        JToken update = Json("{ arr: [1,2,3] }");
        JToken conflict = Json("{ arr: [2,3,1] }");
        JToken origin = Json("{ arr: 'foo' }");
        JToken diff = Json("{ arr: { origin: 'foo', other: [2,3,1], update: [1,2,3] } }");

        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);

        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).True);
        //Assert.That(result, ObjectHas.Property<MergeResult>(x => x.HasConflicts).True
        //                    & ObjectHas.Property<MergeResult>(x => x.Conflicts).Matches(JsonIs.EqualTo(diff)));
    }

    [TestCaseSource(nameof(NonConflictedMerges))]
    public void Merge_WithoutConflicts_Merges(JToken update, JToken conflict, JToken origin, JToken expected)
    {
        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);


        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).False);

        //Assert.That(result, ObjectHas.Property<MergeResult>(x => x.HasConflicts).EqualTo(false)
        //                    & ObjectHas.Property<MergeResult>(x => x.Merged).Matches(JsonIs.EqualTo(expected)));
    }

    [TestCaseSource(nameof(ConflictedMerges))]
    public void Merge_WithConflicts_FailsMerge(JToken update, JToken conflict, JToken origin, JToken expected)
    {
        IJsonDiffMerge differ = new JsonDiffMerge();
        IDiffMergeResult? result = differ.Diff(update, conflict, origin);

        Assert.That(result, Has.Property(nameof(IDiffMergeResult.HasConflicts)).True);
        //Assert.That(result, ObjectHas.Property<MergeResult>(x => x.HasConflicts).EqualTo(true));
        //Assert.That(result.Conflicts, JsonIs.EqualTo(expected));
    }

    public static IEnumerable ConflictedMerges
    {
        get
        {
            yield return Case(
                "{ arr: [1] }",
                "{ arr: [2] }",
                "{ arr: [3] }",
                "{ arr: { update: [1], other: [2], origin: [3] } }"
            );

            yield return Case(
                "{ arr: [1,2] }",
                "{ arr: [2,1] }",
                "{ arr: [3] }",
                "{ arr: { update: [1,2], other: [2,1], origin: [3] } }"
            );

            yield return Case(
                "{ arr: [1] }",
                "{ arr: 42 }",
                "{ arr: [3] }",
                "{ arr: { update: [1], other: 42, origin: [3] } }"
            );

            yield return Case(
                "{ arr: [1] }",
                "{ arr: [2] }",
                "{ }",
                "{ arr: { update: [1], other: [2], origin: null } }"
            );

            yield return Case(
                "{ arr: [1] }",
                "{ arr: [1,2] }",
                "{ arr: [2] }",
                "{ arr: { update: [1], other: [1,2], origin: [2] } }"
            );
        }
    }

    public static IEnumerable NonConflictedMerges
    {
        get
        {
            //Note: All equals
            yield return Case(
                "{ arr: [1,2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [1,2,3] }"
            );

            //Note: Only update changed
            yield return Case(
                "{ arr: [2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [2,3] }"
            );

            //Note: Only conflict changed
            yield return Case(
                "{ arr: [1,2,3] }",
                "{ arr: [2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [2,3] }"
            );

            //Note: Both changed, added item
            yield return Case(
                "{ arr: [1,2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [2,3] }",
                "{ arr: [1,2,3] }"
            );

            //Note: Both changed, removed item
            yield return Case(
                "{ arr: [2,3] }",
                "{ arr: [2,3] }",
                "{ arr: [1,2,3] }",
                "{ arr: [2,3] }"
            );
        }
    }
}