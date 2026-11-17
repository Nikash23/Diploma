using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using XrInterior.ProjectData;

namespace XrInterior.ProjectData.Tests
{
    public class ProjectDataTests
    {
        private static string SamplePath =>
            Path.Combine(Application.dataPath, "_Project/ProjectData/Samples/control_project.json");

        private static string LoadSampleText() => File.ReadAllText(SamplePath);

        [Test]
        public void Sample_Loads_And_IsValid()
        {
            Project p = ProjectSerializer.FromJson(LoadSampleText());
            var errors = ProjectValidator.Validate(p);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(ProjectSource.Template, p.Source);
            Assert.AreEqual(5, p.Walls.Count);
            Assert.AreEqual(2, p.Variants.Count);
        }

        [Test]
        public void RoundTrip_TenCycles_DoesNotChangeData()
        {
            Project p = ProjectSerializer.FromJson(LoadSampleText());
            string first = ProjectSerializer.ToJson(p);
            string current = first;
            for (int i = 0; i < 10; i++)
                current = ProjectSerializer.ToJson(ProjectSerializer.FromJson(current));
            Assert.AreEqual(first, current);
        }

        [Test]
        public void UnknownFields_AreKept()
        {
            JObject o = JObject.Parse(LoadSampleText());
            o["futureField"] = 42;
            o["rooms"][0]["futureRoomField"] = "x";

            Project p = ProjectSerializer.FromJson(o.ToString());
            JObject back = JObject.Parse(ProjectSerializer.ToJson(p));

            Assert.AreEqual(42, (int)back["futureField"]);
            Assert.AreEqual("x", (string)back["rooms"][0]["futureRoomField"]);
        }

        [Test]
        public void NewerSchemaVersion_IsRejected()
        {
            JObject o = JObject.Parse(LoadSampleText());
            o["version"] = 999;
            Assert.Throws<UnsupportedProjectVersionException>(
                () => ProjectSerializer.FromJson(o.ToString()));
        }

        [Test]
        public void BrokenJson_GivesFormatException()
        {
            Assert.Throws<ProjectFormatException>(() => ProjectSerializer.FromJson("{ это не json"));
        }

        [Test]
        public void Validator_FindsBrokenWallReference()
        {
            Project p = ProjectSerializer.FromJson(LoadSampleText());
            p.Openings[0].WallId = "wall_nope";
            var errors = ProjectValidator.Validate(p);
            Assert.IsTrue(errors.Any(e => e.Contains("wall_nope")), string.Join("\n", errors));
        }

        [Test]
        public void Validator_FindsDuplicateId()
        {
            Project p = ProjectSerializer.FromJson(LoadSampleText());
            p.Objects[1].Id = p.Objects[0].Id;
            var errors = ProjectValidator.Validate(p);
            Assert.IsTrue(errors.Any(e => e.Contains("Повторяющийся id")), string.Join("\n", errors));
        }

        [Test]
        public void Validator_FindsOpeningOutsideWall()
        {
            Project p = ProjectSerializer.FromJson(LoadSampleText());
            p.Openings[0].Offset = 3.8f; // дверь 0.9 м на стене 4 м - не помещается
            var errors = ProjectValidator.Validate(p);
            Assert.IsTrue(errors.Any(e => e.Contains("выходит за границы")), string.Join("\n", errors));
        }

        [Test]
        public void Factory_CreatesValidEmptyProject()
        {
            Project p = ProjectFactory.Create("Тест", ProjectSource.Scan);
            var errors = ProjectValidator.Validate(p);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            StringAssert.Contains("\"source\": \"scan\"", ProjectSerializer.ToJson(p));
        }
    }
}
