using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

namespace LC3
{
    [TestFixture]
    class Tester
    {
        private readonly LC3Assembler lc3a = new LC3Assembler();


        [Test]
        public void TestAssemblerData()
        {
            string path = TestContext.CurrentContext.TestDirectory + @"\Tests\Data\";

            foreach (var currentFile in Directory.GetFiles(path, "*.asm"))
            {
                Console.WriteLine("Testing " + currentFile);


                var program = lc3a.Assemble(currentFile).Select(i => i.ToString()).ToArray();


                Assert.IsEmpty(lc3a.Errors); 

                var expectedPath = path + Path.GetFileNameWithoutExtension(currentFile) + ".dat";
                if (File.Exists(expectedPath))
                    Assert.AreEqual(File.ReadAllLines(expectedPath).Where(l => l.Length != 0).ToArray(), program);
            }
        }

        [Test]
        public void TestAssemblerSpecific()
        {
            // Offsets
            lc3a.Assemble(new List<string>()
            {
                "TRAP -128",
                "TRAP 127",
                "TRAP x7F",
                "TRAP 0"
            });
            Assert.IsEmpty(lc3a.Errors);


            lc3a.Assemble(new List<string>()
            {
                "TRAP -129",
                "TRAP 128"
            });
            Assert.AreEqual(lc3a.Errors.Count, 2);
            Assert.AreEqual(lc3a.Errors[0].line, 0);
            Assert.AreEqual(lc3a.Errors[1].line, 1);
        }

        [Test]
        public void TestAssemblerHelperMethods()
        {
            MethodInfo method1 = typeof(LC3Assembler).GetMethod("IsValidOffset", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo method2 = typeof(LC3Assembler).GetMethod("IsValidLabel",  BindingFlags.NonPublic | BindingFlags.Instance);


            Assert.IsTrue(IsValidOffset(0, 4));
            Assert.IsTrue(IsValidOffset(7, 4));
            Assert.IsTrue(IsValidOffset(-8, 4));

            Assert.IsFalse(IsValidOffset(8, 4));
            Assert.IsFalse(IsValidOffset(-9, 4));


            lc3a.Errors.Clear();
            Assert.IsTrue(IsValidLabel("test"));
            Assert.IsTrue(IsValidLabel("test123"));

            Assert.IsFalse(IsValidLabel("and"));
            Assert.IsFalse(IsValidLabel("trap"));

            Assert.IsFalse(IsValidLabel("xtest"));
            Assert.IsFalse(IsValidLabel("123test"));
            Assert.IsFalse(IsValidLabel("123"));
            Assert.AreEqual(3, lc3a.Errors.Count);


            bool IsValidOffset(int offset, int offsetSize) => (bool)method1.Invoke(lc3a, new object[] { offset, offsetSize, "" });
            bool IsValidLabel (string label)               => (bool)method2.Invoke(lc3a, new object[] { label });
        }
    }
}
