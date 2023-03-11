using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.DiffMerge.Test
{
    public class JsonDiffMergeTest
    {
        [Test]
        public void Diff_DifferentObjects_Returns()
        {
            JsonDiffMerge differ = new JsonDiffMerge();

            JObject left = JObject.Parse("{ name: \"peter\" }");
            JObject right = JObject.Parse("{ name: \"hans\" }");
            JObject origin = JObject.Parse("{ name: \"n/a\" }");

            IDiffMergeResult result = differ.Diff(left, right, origin);
        }
    }
}