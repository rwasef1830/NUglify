// TestHelper.cs
//
// Copyright 2010 Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CssUnitTest
{
    sealed class TestHelper
    {
        /// <summary>
        /// regular expression used to remove the testresults path from actual output
        /// </summary>
        private static Regex s_testRunRegex = new Regex(
            @"(/[/*]/#source\s+\d+\s+\d+\s+).+\\TestResults\\[^\\]+(\\.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// the name of the unit test folder under the main project folder
        /// </summary>
        private const string c_unitTestsDataFolder = "TestData\\CSS";
        
        /// <summary>
        /// folder path for input files to tests
        /// </summary>
        private string m_inputFolder;

        /// <summary>
        /// folder path for output files generated by tests
        /// </summary>
        private string m_outputFolder;

        /// <summary>
        /// folder path for expected results to compare against output
        /// </summary>
        private string m_expectedFolder;

        /// <summary>
        /// singleton construct
        /// </summary>
        private static readonly TestHelper m_instance = new TestHelper();
        public static TestHelper Instance
        {
            get { return m_instance; }
        }

        /// <summary>
        /// private constructor so no one outside the class can create an instance
        /// </summary>
        private TestHelper()
        {
            // start with the unit test DLL. All test data folders will be deployed there by testrun configuration.
            // In order to do that, make sure that "Deployment" section in .testrunconfig file contains the "TestData" folder. If
            // this is the case, then everything in the folder will be copied down right next to unit test DLL.
            var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            // Initialize the input, output and expected folders
            m_inputFolder = Path.Combine(Path.Combine(directoryInfo.FullName, c_unitTestsDataFolder), "Input");
            m_outputFolder = Path.Combine(Path.Combine(directoryInfo.FullName, c_unitTestsDataFolder), "Output");
            m_expectedFolder = Path.Combine(Path.Combine(directoryInfo.FullName, c_unitTestsDataFolder), "Expected");

            // output folder may not exist -- create it if it doesn't
            if (!Directory.Exists(m_outputFolder))
            {
                Directory.CreateDirectory(m_outputFolder);
            }

            // input and expected folders should already exists because we
            // check in files under each one
            Trace.WriteLineIf(!Directory.Exists(m_inputFolder), "Input folder does not exist!");
            Trace.WriteLineIf(!Directory.Exists(m_expectedFolder), "Expected folder does not exist!");
        }

        public int RunTest()
        {
            return RunTest(null);
        }

        public int RunTest(string extraArguments, params string[] extraInputs)
        {
            // open the stack trace for this call
            StackTrace stackTrace = new StackTrace();
            string testClass = null;
            string testName = null;

            // save the name of the current method (RunTest)
            string currentMethodName = MethodInfo.GetCurrentMethod().Name;

            // loop from the previous frame up until we get a method name that is not the
            // same as the current method name
            for (int ndx = 1; ndx < stackTrace.FrameCount; ++ndx)
            {
                // get the frame
                StackFrame stackFrame = stackTrace.GetFrame(ndx);

                // we have different entry points with the same name -- we're interested
                // in the first one that ISN'T the same name as our method
                MethodBase methodBase = stackFrame.GetMethod();
                if (methodBase.Name != currentMethodName)
                {
                    // the calling method's name is the test name - we use this as-is for the output file name
                    // and we use any portion before an underscore as the input file
                    testName = methodBase.Name;
                    // get the method's class - we use this as the subfolder under input/output/expected
                    testClass = methodBase.DeclaringType.Name;
                    break;
                }
            }
            // we definitely should be able to find a function on the stack frame that
            // has a different name than this function, but just in case...
            Debug.Assert(testName != null && testClass != null, "Couldn't locate calling stack frame");

            // the output file is just the full test name
            string outputFile = testName;

            // the input file is the portion of the test name before the underscore (if any)
            string inputFile = testName.Split('_')[0];

            // we want to know if the analyze flag is specified in the arguments provided to us
            bool analyzeSpecified = false;

            // create a list we will append all our arguments to
            LinkedList<string> args = new LinkedList<string>();
            if (!string.IsNullOrEmpty(extraArguments))
            {
                // split on spaces
                string[] options = extraArguments.Split(' ');
                // add each one to the args list
                foreach (string option in options)
                {
                    // ignore empty strings
                    if (option.Length > 0)
                    {
                        args.AddLast(option);
                        if (string.Compare(option, "-xml", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            // the next option should be an xml file name, so we'll add an option
                            // that is the test name, the .xml suffix, and scope it to the input path.
                            // set the inputPath variable to this path so we know we are going to use it
                            // as the "input"
                            /*string inputPath = BuildFullPath(
                                m_inputFolder,
                                testClass,
                                inputFile,
                                ".xml",
                                true
                                );
                            args.AddLast(inputPath);
                            outputFiles = ReadXmlForOutputFiles(inputPath, testClass);*/
                            throw new NotImplementedException("NYI");
                        }
                        // the -r option can have a subpart, eg: -res:Strings, so only test to see if
                        // the first two characters of the current option are "-res"
                        else if (option.StartsWith("-res", StringComparison.OrdinalIgnoreCase))
                        {
                            // the next option is a resource file name, so we'll need to scope it to the input path
                            // FIRST we'll try to see if there's an existing compiled .RESOURCES file with the same
                            // name as the current test. eg: if test name is "foo_h", look for foo.resources
                            string resourcePath = BuildFullPath(
                                m_inputFolder,
                                testClass,
                                inputFile,
                                ".resources",
                                false
                                );
                            if (!File.Exists(resourcePath))
                            {
                                // if there's not .RESOURCES file, look for a .RESX file with the same
                                // name as the current test. eg: if test name is "foo_h", look for foo.resx
                                resourcePath = BuildFullPath(
                                    m_inputFolder,
                                    testClass,
                                    inputFile,
                                    ".resx",
                                    false
                                    );
                                if (!File.Exists(resourcePath))
                                {
                                    // doesn't exist!
                                    Assert.Fail(
                                        "Expected resource file does not exist for test '{0}' in folder {1}",
                                        inputFile,
                                        Path.Combine(m_inputFolder, testClass)
                                        );
                                }
                            }
                            args.AddLast(resourcePath);
                        }
                        else if (option.StartsWith("-analyze", StringComparison.OrdinalIgnoreCase))
                        {
                            // yes, we specified the analyze flag
                            analyzeSpecified = true;
                        }
                    }
                }
            }

            // compute the path to the output file
            string outputPath = GetCssPath(
              m_outputFolder,
              testClass,
              outputFile,
              false
              );
            // if it exists already, delete it
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            // if we haven't already added the analyze flag, add it now
            if (!analyzeSpecified)
            {
                args.AddLast("-analyze");
            }

            // add the output parameter
            args.AddLast("-out");
            args.AddLast(outputPath);

            Trace.WriteLine("INPUT FILE(S):");

            // get the input path
            string inputPath = GetCssPath(
              m_inputFolder,
              testClass,
              inputFile,
              true
              );
            // always input the input file
            args.AddLast(inputPath);
            TraceFileContents(inputPath);

            // if there are any extra input files, add them now
            if (extraInputs != null && extraInputs.Length > 0)
            {
                foreach (string extraInput in extraInputs)
                {
                    if (extraInput.Length > 0)
                    {
                        // get the full path
                        inputPath = GetCssPath(
                          m_inputFolder,
                          testClass,
                          extraInput,
                          true
                          );
                        // add it to the list
                        args.AddLast(inputPath);
                        TraceFileContents(inputPath);
                    }
                }
            }

            Trace.WriteLine(string.Empty);

            // create an array of strings the appropriate size
            string[] mainArguments = new string[args.Count];
            // copy the arguments to the array
            args.CopyTo(mainArguments, 0);

            StringBuilder sb = new StringBuilder();
            foreach (string arg in mainArguments)
            {
                sb.Append(' ');
                sb.Append(arg);
            }
            Trace.WriteLine(string.Empty);
            Trace.WriteLine("COMMAND LINE ARGUMENTS:");
            Trace.WriteLine(sb.ToString());

            // call the CSSCRUNCH main function
            Trace.WriteLine(string.Empty);
            Trace.WriteLine("CSSCRUNCH Debug:");
            int retValue = Microsoft.Ajax.Utilities.MainClass.Main(mainArguments);

            // after the run, the output file BETTER exist...
            if (File.Exists(outputPath))
            {
                // compute the path to the expected file
                string expectedPath = GetCssPath(
                  m_expectedFolder,
                  testClass,
                  outputFile,
                  true
                  );

                Trace.WriteLine(string.Empty);
                Trace.WriteLine("odd \"" + expectedPath + "\" \"" + outputPath + "\"");

                Trace.WriteLine(string.Empty);
                Trace.WriteLine("EXPECTED OUTPUT FILE:");
                // trace output contents
                TraceFileContents(expectedPath);

                Trace.WriteLine(string.Empty);
                Trace.WriteLine("ACTUAL OUTPUT FILE:");
                // trace output contents
                TraceFileContents(outputPath);

                // fail the test if the files do not match
                Assert.IsTrue(CompareTextFiles(outputPath, expectedPath), "The expected output ({1}) and actual output ({0}) do not match!", outputPath, expectedPath);
            }
            else
            {
                // no output file....
                Assert.Fail("Output file ({0}) does not exist.", outputPath);
            }

            return retValue;
        }

        // start with root folder, add subfolder, then add the file name + ".css" extension.
        private string GetCssPath(string rootFolder, string subfolder, string fileName, bool mustExist)
        {
            return BuildFullPath(rootFolder, subfolder, fileName, ".CSS", mustExist);
        }

        // start with root folder, add subfolder, then add the file name + extension.
        private string BuildFullPath(string rootFolder, string subfolder, string fileName, string extension, bool mustExist)
        {
            string folderPath = Path.Combine(rootFolder, subfolder);
            string fullPath = Path.ChangeExtension(Path.Combine(folderPath, fileName), extension);
            if (mustExist)
            {
                Assert.IsTrue(
                  File.Exists(fullPath),
                  string.Format("Expected file does not exist: {0}", fullPath)
                  );
            }
            return fullPath;
        }

        private void TraceFileContents(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string text = s_testRunRegex.Replace(reader.ReadToEnd(), "$1TESTRUNPATH$2");

                Trace.WriteLine(filePath);
                Trace.WriteLine(text);
                Trace.WriteLine(string.Empty);
            }
        }

        private bool CompareTextFiles(string leftPath, string rightPath)
        {
            Debug.Assert(File.Exists(leftPath));
            Debug.Assert(File.Exists(rightPath));

            using (StreamReader leftReader = new StreamReader(leftPath))
            {
                using (StreamReader rightReader = new StreamReader(rightPath))
                {
                    string left = s_testRunRegex.Replace(leftReader.ReadToEnd(), "$1TESTRUNPATH$2");
                    string right = s_testRunRegex.Replace(rightReader.ReadToEnd(), "$1TESTRUNPATH$2");

                    return (string.Compare(left, right) == 0);
                }
            }
        }
    }
}