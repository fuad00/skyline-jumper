using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StickmanJumper.EditorTools;
using StickmanJumper.Presentation;
using UnityEngine;

namespace StickmanJumper.Tests
{
    public class CharacterClipTests
    {
        static CharacterClip.Key Key(int frame, float x, float rot) =>
            new CharacterClip.Key { Frame = frame, Position = new Vector2(x, 0f), Rotation = rot, Scale = Vector2.one };

        [Test]
        public void Sample_InterpolatesBetweenKeys_AndHoldsAtTheEnds()
        {
            var keys = new[] { Key(0, 0f, 10f), Key(10, 20f, 356f), Key(20, 20f, 383f) };
            Assert.IsTrue(CharacterClip.Sample(keys, 5f, out var mid));
            Assert.AreEqual(10f, mid.Position.x, 1e-4f);
            Assert.AreEqual(183f, mid.Rotation, 1e-3f, "rotations are stored unwrapped: plain lerp");
            CharacterClip.Sample(keys, 15f, out var late);
            Assert.AreEqual(369.5f, late.Rotation, 1e-3f);
            CharacterClip.Sample(keys, -3f, out var before);
            Assert.AreEqual(0f, before.Position.x);
            CharacterClip.Sample(keys, 99f, out var after);
            Assert.AreEqual(383f, after.Rotation);
            Assert.IsFalse(CharacterClip.Sample(new CharacterClip.Key[0], 1f, out _));
        }

        [Test]
        public void FrameAt_LoopsOrHolds()
        {
            var clip = ScriptableObject.CreateInstance<CharacterClip>();
            clip.FrameTime = 0.1f;
            clip.LastFrame = 10;
            Assert.AreEqual(1f, clip.Duration, 1e-5f);
            Assert.AreEqual(5f, clip.FrameAt(1.5f, true), 1e-4f);
            Assert.AreEqual(10f, clip.FrameAt(1.5f, false), 1e-4f);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void DrawOrder_NegativeZUnderParent_OthersOnTop()
        {
            var clip = ScriptableObject.CreateInstance<CharacterClip>();
            clip.Bones = new[]
            {
                new CharacterClip.Bone { Name = "root", Parent = -1 },
                new CharacterClip.Bone { Name = "body", Parent = 0, Z = 0f },
                new CharacterClip.Bone { Name = "armFront", Parent = 1, Z = 2f },
                new CharacterClip.Bone { Name = "armBack", Parent = 1, Z = -3f },
                new CharacterClip.Bone { Name = "legBack", Parent = 1, Z = -2f },
                new CharacterClip.Bone { Name = "head", Parent = 1, Z = 1f },
            };
            var names = clip.DrawOrder().Select(i => clip.Bones[i].Name).ToArray();
            CollectionAssert.AreEqual(new[] { "root", "armBack", "legBack", "body", "head", "armFront" }, names);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ParsePlist_ReadsDictsArraysAndStrings()
        {
            const string xml = "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>FrameTime</key><string>33</string>" +
                               "<key>list</key><array><string>a</string><dict><key>k</key><string>v</string></dict></array></dict></plist>";
            var root = (Dictionary<string, object>)CharacterImporter.ParsePlist(xml);
            Assert.AreEqual("33", root["FrameTime"]);
            var list = (List<object>)root["list"];
            Assert.AreEqual("a", list[0]);
            Assert.AreEqual("v", ((Dictionary<string, object>)list[1])["k"]);
        }
    }

    /// <summary>Проверки импортированных анимаций оригинала (пропускаются, если их нет — свежий клон без reference/).</summary>
    public class CharacterImportTests
    {
        static readonly string[] Files =
            { "stuntmanStand", "stuntmanWalk", "stuntmanRun", "stuntmanJump", "stuntmanParachute", "stuntmanFastLanding", "stuntmanWins", "stuntmanLoose" };

        static CharacterClip Load(string file)
        {
            var clip = CharacterImporter.LoadClip(file);
            if (clip == null) Assert.Ignore("original animations are not imported (no reference/decoded)");
            return clip;
        }

        [Test]
        public void AllAnimations_HaveBodyWithPictures()
        {
            foreach (var file in Files)
            {
                var clip = Load(file);
                Assert.Greater(clip.LastFrame, 0, file);
                Assert.Greater(clip.FrameTime, 0f, file);
                foreach (var bone in new[] { "Body0", "head", "upperLegF", "lowerLegF", "upperArmF" })
                {
                    int i = clip.IndexOf(bone);
                    Assert.GreaterOrEqual(i, 0, $"{file}: no {bone}");
                    Assert.IsTrue(clip.Bones[i].Keys.All(k => k.Sprite != null), $"{file}: {bone} has a key without a picture");
                }
            }
        }

        /// <summary>Стойка оригинала ≈ 83 px ростом, ноги на ≈ 37 px ниже корня скелета (сверено с картинкой кадров).</summary>
        [Test]
        public void Stand_HeightAndGround_MatchTheOriginal()
        {
            var stand = Load("stuntmanStand");
            Assert.AreEqual(83.4f, stand.Height, 1.5f);
            Assert.AreEqual(-37.3f, stand.GroundY, 1.5f);
        }

        /// <summary>Стоит прямо: голова над ногами, а не сбоку (так ловится неверное направление поворота).</summary>
        [Test]
        public void Stand_IsUpright()
        {
            var stand = Load("stuntmanStand");
            var pose = stand.PoseMatrices(0f, out _);
            Vector2 head = pose[stand.IndexOf("head")].MultiplyPoint3x4(Vector3.zero);
            Vector2 knee = pose[stand.IndexOf("lowerLegF")].MultiplyPoint3x4(Vector3.zero);
            Assert.Greater(head.y - knee.y, 30f, "head well above the knee (≈36 px; the wrong rotation direction gives ≈19)");
            Assert.Less(Mathf.Abs(head.x - knee.x), 12f, "head roughly above the legs");
        }

        [Test]
        public void Parachute_HasCanopyAboveTheHead()
        {
            var clip = Load("stuntmanParachute");
            var pose = clip.PoseMatrices(0f, out var visible);
            int cell = clip.IndexOf("3"), head = clip.IndexOf("head");
            Assert.IsTrue(visible[cell]);
            float above = pose[cell].MultiplyPoint3x4(Vector3.zero).y - pose[head].MultiplyPoint3x4(Vector3.zero).y;
            Assert.Greater(above, 150f, "the canopy hangs high on long lines, like in the original video");
        }
    }
}
