using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using XrInterior.ProjectData;

namespace XrInterior.ProjectData.Tests
{
    public class ProjectStorageTests
    {
        private string _dir;

        private static string SamplePath =>
            Path.Combine(Application.dataPath, "_Project/ProjectData/Samples/control_project.json");

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "xri_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private ProjectService NewService() { return new ProjectService(new ProjectStorage(_dir)); }

        private static string WithoutUpdatedAt(string json)
        {
            JObject o = JObject.Parse(json);
            o.Remove("updatedAt");
            return o.ToString();
        }

        [Test]
        public void Create_SaveLoad_Roundtrip()
        {
            var svc = NewService();
            int saved = 0, loaded = 0;
            svc.ProjectSaved += () => saved++;
            svc.ProjectLoaded += p => loaded++;

            Project created = svc.Create("Мой проект", ProjectSource.Template, "building_main");
            Assert.AreEqual(1, loaded);
            svc.Save();
            Assert.AreEqual(1, saved);

            var svc2 = NewService();
            Project back = svc2.Load(created.Id);
            Assert.AreEqual("Мой проект", back.Name);
            Assert.AreEqual("building_main", back.BuildingId);
            Assert.AreEqual(ProjectSource.Template, back.Source);
        }

        [Test]
        public void FullCycle_ThreeProjects()
        {
            var svc = NewService();
            Project p1 = svc.Create("P1", ProjectSource.Template);
            Project p2 = svc.Create("P2", ProjectSource.Scan);
            Project p3 = svc.Create("P3", ProjectSource.Bim);
            Assert.AreEqual(3, svc.ListProjects().Count);

            svc.Rename(p2.Id, "Второй");
            Assert.AreEqual("Второй", NewService().Load(p2.Id).Name);

            svc.Delete(p1.Id);
            Assert.AreEqual(2, svc.ListProjects().Count);
            Assert.Throws<ProjectNotFoundException>(() => NewService().Load(p1.Id));
            Assert.IsNotNull(p3);
        }

        [Test]
        public void TenSaveCycles_DoNotChangeData()
        {
            var svc = NewService();
            svc.Import(SamplePath);
            string id = svc.Current.Id;
            string first = WithoutUpdatedAt(ProjectSerializer.ToJson(svc.Current));

            for (int i = 0; i < 10; i++)
            {
                svc.Save();
                svc = NewService();
                svc.Load(id);
            }

            Assert.AreEqual(first, WithoutUpdatedAt(ProjectSerializer.ToJson(svc.Current)));
        }

        [Test]
        public void InterruptedWrite_RestoresFromBackup()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            string id = p.Id;
            svc.Current.Name = "B";
            svc.Save(); // теперь в .bak лежит версия "A"

            var storage = new ProjectStorage(_dir);
            File.WriteAllText(storage.MainPath(id), "{ обрыв записи");

            var svc2 = new ProjectService(storage);
            Project loaded = svc2.Load(id);
            Assert.AreEqual("A", loaded.Name);

            // основной файл восстановлен
            Assert.AreEqual("A", ProjectSerializer.FromJson(File.ReadAllText(storage.MainPath(id))).Name);
        }

        [Test]
        public void LeftoverTempFile_IsIgnored()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            var storage = new ProjectStorage(_dir);
            File.WriteAllText(storage.TempPath(p.Id), "{ недописанный файл");

            Project loaded = new ProjectService(storage).Load(p.Id);
            Assert.AreEqual("A", loaded.Name);
        }

        [Test]
        public void CrashAfterDelete_RestoresFromTemp()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            var storage = new ProjectStorage(_dir);
            File.Copy(storage.MainPath(p.Id), storage.TempPath(p.Id), true);
            File.Delete(storage.MainPath(p.Id));

            Project loaded = new ProjectService(storage).Load(p.Id);
            Assert.AreEqual("A", loaded.Name);
            Assert.IsTrue(File.Exists(storage.MainPath(p.Id)));
        }

        [Test]
        public void NewerVersion_IsNotReplacedByBackup()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            svc.Save(); // появляется .bak

            var storage = new ProjectStorage(_dir);
            JObject o = JObject.Parse(File.ReadAllText(storage.MainPath(p.Id)));
            o["version"] = 999;
            File.WriteAllText(storage.MainPath(p.Id), o.ToString());

            Assert.Throws<UnsupportedProjectVersionException>(() => new ProjectService(storage).Load(p.Id));
        }

        [Test]
        public void MarkChanged_AutoSaves_AndRaisesEvents()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            int changed = 0, saved = 0;
            svc.ProjectChanged += () => changed++;
            svc.ProjectSaved += () => saved++;

            svc.Current.Name = "Новое";
            svc.MarkChanged();

            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, saved);
            Assert.AreEqual("Новое", NewService().Load(p.Id).Name);
        }

        [Test]
        public void ExportImport_CreatesSeparateCopy()
        {
            var svc = NewService();
            Project p = svc.Create("A", ProjectSource.Template);
            string exportDir = Path.Combine(_dir, "export");

            string path = svc.Export(p.Id, exportDir);
            Assert.IsTrue(File.Exists(path));

            Project imported = svc.Import(path);
            Assert.AreNotEqual(p.Id, imported.Id);
            Assert.AreEqual(2, svc.ListProjects().Count);
        }

        [Test]
        public void UnknownProject_ThrowsNotFound()
        {
            Assert.Throws<ProjectNotFoundException>(() => NewService().Load("project_net_takogo"));
        }

        [Test]
        public void DangerousId_IsRejected()
        {
            var storage = new ProjectStorage(_dir);
            Assert.Throws<ArgumentException>(() => storage.Read("../x"));
        }
    }
}
