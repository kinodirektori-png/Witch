using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Witch.Data;
using Witch.UI;

namespace Witch.Tests
{
    /// <summary>
    /// Guards for the App Store Kids Category and the "no data collected" privacy label (#22). These read the
    /// project files, so adding an ads or analytics package, or an unguarded link, fails the build.
    /// </summary>
    public class KidSafetyTests
    {
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static readonly string[] BannedPackages =
        {
            "com.unity.ads",
            "com.unity.advertisement",
            "com.unity.analytics",
            "com.unity.services.analytics",
            "com.unity.services.core",
            "com.unity.purchasing",
            "com.unity.services.cloud-diagnostics",
            "com.google.firebase",
            "com.google.ads",
            "com.facebook",
        };

        /// <summary>APIs that collect data, track, or reach outside the game.</summary>
        private static readonly string[] BannedApis =
        {
            "UnityEngine.Analytics",
            "UnityWebRequest",
            "deviceUniqueIdentifier",
            "advertisingIdentifier",
            "RequestAdvertisingIdentifierAsync",
            "Social.localUser",
            "Input.location",
            "WebCamTexture",
            "Microphone.Start",
        };

        private static IEnumerable<string> ScriptFiles()
        {
            return Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);
        }

        [Test]
        public void NoAdsOrAnalyticsPackages()
        {
            string manifest = File.ReadAllText(Path.Combine(ProjectRoot, "Packages", "manifest.json"));
            var found = new List<string>();
            foreach (string package in BannedPackages)
            {
                if (manifest.Contains($"\"{package}"))
                {
                    found.Add(package);
                }
            }

            Assert.IsEmpty(found, "Kids Category: no ads, analytics or purchases. Found: " + string.Join(", ", found));
        }

        [Test]
        public void NoTrackingOrNetworkApisInGameCode()
        {
            var found = new List<string>();
            foreach (string file in ScriptFiles())
            {
                string code = File.ReadAllText(file);
                foreach (string api in BannedApis)
                {
                    if (code.Contains(api))
                    {
                        found.Add($"{Path.GetFileName(file)}: {api}");
                    }
                }
            }

            Assert.IsEmpty(found, "The privacy label says no data is collected. Found:\n" + string.Join("\n", found));
        }

        [Test]
        public void LinksOnlyOpenThroughTheParentalGate()
        {
            var found = new List<string>();
            foreach (string file in ScriptFiles())
            {
                if (Path.GetFileName(file) == "ExternalLink.cs")
                {
                    continue;
                }

                if (File.ReadAllText(file).Contains("OpenURL"))
                {
                    found.Add(Path.GetFileName(file));
                }
            }

            Assert.IsEmpty(found, "Open links with the ExternalLink component (it asks the parental gate). Found in: " +
                                  string.Join(", ", found));
        }

        [Test]
        public void ParentalGate_AcceptsOnlyTheRightNumber()
        {
            var challenge = new ParentalGateChallenge(7, 8);

            Assert.AreEqual("What is seven times eight?", challenge.Question);
            Assert.IsTrue(challenge.IsCorrect("56"));
            Assert.IsTrue(challenge.IsCorrect(" 56 "));
            Assert.IsFalse(challenge.IsCorrect("55"));
            Assert.IsFalse(challenge.IsCorrect("fifty-six"));
            Assert.IsFalse(challenge.IsCorrect(""));
            Assert.IsFalse(challenge.IsCorrect(null));
        }

        [Test]
        public void ParentalGate_RandomQuestionsStayInRange()
        {
            var random = new System.Random(1234);
            for (int i = 0; i < 200; i++)
            {
                ParentalGateChallenge challenge = ParentalGateChallenge.CreateRandom(random);
                Assert.That(challenge.FactorA, Is.InRange(ParentalGateChallenge.MinFactor, ParentalGateChallenge.MaxFactor));
                Assert.That(challenge.FactorB, Is.InRange(ParentalGateChallenge.MinFactor, ParentalGateChallenge.MaxFactor));
                Assert.That(challenge.Answer, Is.GreaterThanOrEqualTo(36), "Too easy for an adult-only question.");
            }
        }

        [Test]
        public void ParentalGate_RejectsEasyFactors()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ParentalGateChallenge(2, 3));
        }

        [Test]
        public void SafetyNote_IsAddedToTheExplanation()
        {
            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            try
            {
                recipe.conditions.whatYouSee = "It fizzes.";
                Assert.AreEqual("It fizzes.", recipe.KidExplanationWithSafety);

                recipe.safetyNote = "Ask an adult.";
                Assert.AreEqual("It fizzes.\n<b>Safety:</b> Ask an adult.", recipe.KidExplanationWithSafety);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
            }
        }
    }
}
