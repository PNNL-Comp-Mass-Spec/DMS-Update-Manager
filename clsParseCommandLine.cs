using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DMSUpdateManager
{
    // This class can be used to parse the text following the program name when a
    //  program is started from the command line
    //
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Program started November 8, 2003

    // E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
    // Website: http://panomics.pnnl.gov/ or http://www.sysbio.org/resources/staff/
    // -------------------------------------------------------------------------------
    //
    // Licensed under the Apache License, Version 2.0; you may not use this file except
    // in compliance with the License.  You may obtain a copy of the License at
    // http://www.apache.org/licenses/LICENSE-2.0
    //
    // Notice: This computer software was prepared by Battelle Memorial Institute,
    // hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the
    // Department of Energy (DOE).  All rights in the computer software are reserved
    // by DOE on behalf of the United States Government and the Contractor as
    // provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY
    // WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS
    // SOFTWARE.  This notice including this sentence must appear on any copies of
    // this computer software.

    //
    // Last modified March 27, 2017

    public class clsParseCommandLine
    {
        public const char DEFAULT_SWITCH_CHAR = '/';

        public const char ALTERNATE_SWITCH_CHAR = '-';

        public const char DEFAULT_SWITCH_PARAM_CHAR = ':';
        private readonly Dictionary<string, string> mSwitches = new Dictionary<string, string>();

        private readonly List<string> mNonSwitchParameters = new List<string>();

        private bool mShowHelp = false;

        public bool NeedToShowHelp
        {
            get { return mShowHelp; }
        }

        // ReSharper disable once UnusedMember.Global
        public int ParameterCount
        {
            get { return mSwitches.Count; }
        }

        // ReSharper disable once UnusedMember.Global
        public int NonSwitchParameterCount
        {
            get { return mNonSwitchParameters.Count; }
        }

        public bool DebugMode { get; }

        public clsParseCommandLine(bool blnDebugMode = false)
        {
            DebugMode = blnDebugMode;
        }

        /// <summary>
        /// Compares the parameter names in objParameterList with the parameters at the command line
        /// </summary>
        /// <param name="objParameterList">Parameter list</param>
        /// <returns>True if any of the parameters are not present in strParameterList()</returns>
        public bool InvalidParametersPresent(List<string> objParameterList)
        {
            const bool blnCaseSensitive = false;
            return InvalidParametersPresent(objParameterList, blnCaseSensitive);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Compares the parameter names in strParameterList with the parameters at the command line
        /// </summary>
        /// <param name="strParameterList">Parameter list</param>
        /// <returns>True if any of the parameters are not present in strParameterList()</returns>
        public bool InvalidParametersPresent(string[] strParameterList)
        {
            const bool blnCaseSensitive = false;
            return InvalidParametersPresent(strParameterList, blnCaseSensitive);
        }

        /// <summary>
        /// Compares the parameter names in strParameterList with the parameters at the command line
        /// </summary>
        /// <param name="strParameterList">Parameter list</param>
        /// <param name="blnCaseSensitive">True to perform case-sensitive matching of the parameter name</param>
        /// <returns>True if any of the parameters are not present in strParameterList()</returns>
        public bool InvalidParametersPresent(string[] strParameterList, bool blnCaseSensitive)
        {
            if (InvalidParameters(strParameterList.ToList(), blnCaseSensitive).Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool InvalidParametersPresent(List<string> lstValidParameters, bool blnCaseSensitive)
        {
            if (InvalidParameters(lstValidParameters, blnCaseSensitive).Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public List<string> InvalidParameters(List<string> lstValidParameters)
        {
            const bool blnCaseSensitive = false;
            return InvalidParameters(lstValidParameters, blnCaseSensitive);
        }

        public List<string> InvalidParameters(List<string> lstValidParameters, bool blnCaseSensitive)
        {
            var lstInvalidParameters = new List<string>();

            try
            {
                // Find items in mSwitches whose keys are not in lstValidParameters)

                foreach (KeyValuePair<string, string> item in mSwitches)
                {
                    string itemKey = item.Key;
                    int intMatchCount = 0;

                    if (blnCaseSensitive)
                    {
                        intMatchCount = (from validItem in lstValidParameters where validItem == itemKey select validItem).Count();
                    }
                    else
                    {
                        intMatchCount = (from validItem in lstValidParameters where string.Equals(validItem, itemKey,
                            StringComparison.InvariantCultureIgnoreCase) select validItem).Count();
                    }

                    if (intMatchCount == 0)
                    {
                        lstInvalidParameters.Add(item.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in InvalidParameters", ex);
            }

            return lstInvalidParameters;
        }

        /// <summary>
        /// Look for parameter on the command line
        /// </summary>
        /// <param name="strParameterName">Parameter name</param>
        /// <returns>True if present, otherwise false</returns>
        /// <remarks>Does not work for /? or /help -- for those, use .NeedToShowHelp</remarks>
        public bool IsParameterPresent(string strParameterName)
        {
            string strValue = string.Empty;
            const bool blnCaseSensitive = false;
            return RetrieveValueForParameter(strParameterName, out strValue, blnCaseSensitive);
        }

        /// <summary>
        /// Parse the parameters and switches at the command line; uses / for the switch character and : for the switch parameter character
        /// </summary>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>If /? or /help is found, then returns False and sets mShowHelp to True</remarks>
        public bool ParseCommandLine()
        {
            return ParseCommandLine(DEFAULT_SWITCH_CHAR, DEFAULT_SWITCH_PARAM_CHAR);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Parse the parameters and switches at the command line; uses : for the switch parameter character
        /// </summary>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>If /? or /help is found, then returns False and sets mShowHelp to True</remarks>
        public bool ParseCommandLine(char strSwitchStartChar)
        {
            return ParseCommandLine(strSwitchStartChar, DEFAULT_SWITCH_PARAM_CHAR);
        }

        /// <summary>
        /// Parse the parameters and switches at the command line
        /// </summary>
        /// <param name="chSwitchStartChar"></param>
        /// <param name="chSwitchParameterChar"></param>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>If /? or /help is found, then returns False and sets mShowHelp to True</remarks>
        public bool ParseCommandLine(char chSwitchStartChar, char chSwitchParameterChar)
        {
            // Returns True if any command line parameters were found
            // Otherwise, returns false
            //
            // If /? or /help is found, then returns False and sets mShowHelp to True

            string strCmdLine = null;

            mSwitches.Clear();
            mNonSwitchParameters.Clear();

            try
            {
                try
                {
                    // .CommandLine() returns the full command line
                    strCmdLine = Environment.CommandLine;

                    // .GetCommandLineArgs splits the command line at spaces, though it keeps text between double quotes together
                    // Note that .NET will strip out the starting and ending double quote if the user provides a parameter like this:
                    // MyProgram.exe "C:\Program Files\FileToProcess"
                    //
                    // In this case, strParameters(1) will not have a double quote at the start but it will have a double quote at the end:
                    //  strParameters(1) = C:\Program Files\FileToProcess"

                    // One very odd feature of Environment.GetCommandLineArgs() is that if the command line looks like this:
                    //    MyProgram.exe "D:\My Folder\Subfolder\" /O:D:\OutputFolder
                    // Then strParameters will have:
                    //    strParameters(1) = D:\My Folder\Subfolder" /O:D:\OutputFolder
                    //
                    // To avoid this problem instead specify the command line as:
                    //    MyProgram.exe "D:\My Folder\Subfolder" /O:D:\OutputFolder
                    // which gives:
                    //    strParameters(1) = D:\My Folder\Subfolder
                    //    strParameters(2) = /O:D:\OutputFolder
                    //
                    // Due to the idiosyncrasies of .GetCommandLineArgs, we will instead use SplitCommandLineParams to do the splitting
                    // strParameters = Environment.GetCommandLineArgs()
                }
                catch (Exception ex)
                {
                    // In .NET 1.x, programs would fail if called from a network share
                    // This appears to be fixed in .NET 2.0 and above
                    // If an exception does occur here, we'll show the error message at the console, then sleep for 2 seconds

                    Console.WriteLine("------------------------------------------------------------------------------");
                    Console.WriteLine("This program cannot be run from a network share.  Please map a drive to the");
                    Console.WriteLine(" network share you are currently accessing or copy the program files and");
                    Console.WriteLine(" required DLL's to your local computer.");
                    Console.WriteLine(" Exception: " + ex.Message);
                    Console.WriteLine("------------------------------------------------------------------------------");

                    PauseAtConsole(5000, 1000);

                    mShowHelp = true;
                    return false;
                }

                if (DebugMode)
                {
                    Console.WriteLine();
                    Console.WriteLine("Debugging command line parsing");
                    Console.WriteLine();
                }

                var strParameters = SplitCommandLineParams(strCmdLine);

                if (DebugMode)
                {
                    Console.WriteLine();
                }

                if (string.IsNullOrWhiteSpace(strCmdLine))
                {
                    return false;
                }
                else if (strCmdLine.IndexOf(chSwitchStartChar + "?", StringComparison.Ordinal) > 0 ||
                         strCmdLine.ToLower().IndexOf(chSwitchStartChar + "help", StringComparison.Ordinal) > 0)
                {
                    mShowHelp = true;
                    return false;
                }

                // Parse the command line
                // Note that strParameters(0) is the path to the Executable for the calling program

                for (var intIndex = 1; intIndex <= strParameters.Length - 1; intIndex++)
                {
                    if (strParameters[intIndex].Length > 0)
                    {
                        var strKey = strParameters[intIndex].TrimStart(' ');
                        var strValue = string.Empty;
                        bool blnSwitchParam = false;

                        if (strKey.StartsWith(chSwitchStartChar.ToString()))
                        {
                            blnSwitchParam = true;
                        }
                        else if (strKey.StartsWith(ALTERNATE_SWITCH_CHAR.ToString()) || strKey.StartsWith(DEFAULT_SWITCH_CHAR.ToString()))
                        {
                            blnSwitchParam = true;
                        }
                        else
                        {
                            // Parameter doesn't start with strSwitchStartChar or / or -
                            blnSwitchParam = false;
                        }

                        if (blnSwitchParam)
                        {
                            // Look for strSwitchParameterChar in strParameters(intIndex)
                            var intCharIndex = strParameters[intIndex].IndexOf(chSwitchParameterChar);

                            if (intCharIndex >= 0)
                            {
                                // Parameter is of the form /I:MyParam or /I:"My Parameter" or -I:"My Parameter" or /MyParam:Setting
                                strValue = strKey.Substring(intCharIndex + 1).Trim();

                                // Remove any starting and ending quotation marks
                                strValue = strValue.Trim('"');

                                strKey = strKey.Substring(0, intCharIndex);
                            }
                            else
                            {
                                // Parameter is of the form /S or -S
                            }

                            // Remove the switch character from strKey
                            strKey = strKey.Substring(1).Trim();

                            if (DebugMode)
                            {
                                Console.WriteLine("SwitchParam: " + strKey + "=" + strValue);
                            }

                            // Note: .Item() will add strKey if it doesn't exist (which is normally the case)
                            mSwitches[strKey] = strValue;
                        }
                        else
                        {
                            // Non-switch parameter since strSwitchParameterChar was not found and does not start with strSwitchStartChar

                            // Remove any starting and ending quotation marks
                            strKey = strKey.Trim('"');

                            if (DebugMode)
                            {
                                Console.WriteLine("NonSwitchParam " + mNonSwitchParameters.Count + ": " + strKey);
                            }

                            mNonSwitchParameters.Add(strKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in ParseCommandLine", ex);
            }

            if (DebugMode)
            {
                Console.WriteLine();
                Console.WriteLine("Switch Count = " + mSwitches.Count);
                Console.WriteLine("NonSwitch Count = " + mNonSwitchParameters.Count);
                Console.WriteLine();
            }

            if (mSwitches.Count + mNonSwitchParameters.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void PauseAtConsole(int intMillisecondsToPause, int intMillisecondsBetweenDots)
        {
            int intIteration = 0;
            int intTotalIterations = 0;

            Console.WriteLine();
            Console.Write("Continuing in " + (intMillisecondsToPause / 1000.0).ToString("0") + " seconds ");

            try
            {
                if (intMillisecondsBetweenDots == 0)
                    intMillisecondsBetweenDots = intMillisecondsToPause;

                intTotalIterations = Convert.ToInt32(Math.Round((double)intMillisecondsToPause / intMillisecondsBetweenDots, 0.0));
            }
            catch (Exception)
            {
                intTotalIterations = 1;
            }

            intIteration = 0;
            do
            {
                Console.Write('.');

                Thread.Sleep(intMillisecondsBetweenDots);

                intIteration += 1;
            } while (intIteration < intTotalIterations);

            Console.WriteLine();
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Returns the value of the non-switch parameter at the given index
        /// </summary>
        /// <param name="intParameterIndex">Parameter index</param>
        /// <returns>The value of the parameter at the given index; empty string if no value or invalid index</returns>
        public string RetrieveNonSwitchParameter(int intParameterIndex)
        {
            string strValue = string.Empty;

            if (intParameterIndex < mNonSwitchParameters.Count)
            {
                strValue = mNonSwitchParameters[intParameterIndex];
            }

            if (strValue == null)
            {
                strValue = string.Empty;
            }

            return strValue;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Returns the parameter at the given index
        /// </summary>
        /// <param name="intParameterIndex">Parameter index</param>
        /// <param name="strKey">Parameter name (output)</param>
        /// <param name="strValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <returns></returns>
        public bool RetrieveParameter(int intParameterIndex, out string strKey, out string strValue)
        {
            int intIndex = 0;

            try
            {
                strKey = string.Empty;
                strValue = string.Empty;

                if (intParameterIndex < mSwitches.Count)
                {
                    Dictionary<string, string>.Enumerator iEnum = mSwitches.GetEnumerator();

                    intIndex = 0;
                    while (iEnum.MoveNext())
                    {
                        if (intIndex == intParameterIndex)
                        {
                            strKey = iEnum.Current.Key;
                            strValue = iEnum.Current.Value;
                            return true;
                        }
                        intIndex += 1;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in RetrieveParameter", ex);
            }

            return false;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Look for parameter on the command line and returns its value in strValue
        /// </summary>
        /// <param name="strKey">Parameter name</param>
        /// <param name="strValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <returns>True if present, otherwise false</returns>
        public bool RetrieveValueForParameter(string strKey, out string strValue)
        {
            return RetrieveValueForParameter(strKey, out strValue, false);
        }

        /// <summary>
        /// Look for parameter on the command line and returns its value in strValue
        /// </summary>
        /// <param name="strKey">Parameter name</param>
        /// <param name="strValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <param name="blnCaseSensitive">True to perform case-sensitive matching of the parameter name</param>
        /// <returns>True if present, otherwise false</returns>
        public bool RetrieveValueForParameter(string strKey, out string strValue, bool blnCaseSensitive)
        {
            try
            {
                strValue = string.Empty;

                if (blnCaseSensitive)
                {
                    if (mSwitches.ContainsKey(strKey))
                    {
                        strValue = Convert.ToString(mSwitches[strKey]);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    var query = (from item in mSwitches where string.Equals(item.Key, strKey, StringComparison.InvariantCultureIgnoreCase) select item)
                        .ToList();

                    if (query.Count == 0)
                    {
                        return false;
                    }

                    strValue = query.FirstOrDefault().Value;
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in RetrieveValueForParameter", ex);
            }
        }

        private string[] SplitCommandLineParams(string strCmdLine)
        {
            List<string> strParameters = new List<string>();
            string strParameter = null;

            var intIndexStart = 0;
            var intIndexEnd = 0;
            bool blnInsideDoubleQuotes = false;

            try
            {
                if (!string.IsNullOrEmpty(strCmdLine))
                {
                    // Make sure the command line doesn't have any carriage return or linefeed characters
                    strCmdLine = strCmdLine.Replace("\r\n", " ");
                    strCmdLine = strCmdLine.Replace("\r", " ");
                    strCmdLine = strCmdLine.Replace("\n", " ");

                    blnInsideDoubleQuotes = false;

                    while (intIndexStart < strCmdLine.Length)
                    {
                        // Step through the characters to find the next space
                        // However, if we find a double quote, then stop checking for spaces

                        if (strCmdLine[intIndexEnd] == '"')
                        {
                            blnInsideDoubleQuotes = !blnInsideDoubleQuotes;
                        }

                        if (!blnInsideDoubleQuotes || intIndexEnd == strCmdLine.Length - 1)
                        {
                            if (strCmdLine[intIndexEnd] == ' ' || intIndexEnd == strCmdLine.Length - 1)
                            {
                                // Found the end of a parameter
                                strParameter = strCmdLine.Substring(intIndexStart, intIndexEnd - intIndexStart + 1).TrimEnd(' ');

                                if (strParameter.StartsWith("\""))
                                {
                                    strParameter = strParameter.Substring(1);
                                }

                                if (strParameter.EndsWith("\""))
                                {
                                    strParameter = strParameter.Substring(0, strParameter.Length - 1);
                                }

                                if (!string.IsNullOrEmpty(strParameter))
                                {
                                    if (DebugMode)
                                    {
                                        Console.WriteLine("Param " + strParameters.Count + ": " + strParameter);
                                    }
                                    strParameters.Add(strParameter);
                                }

                                intIndexStart = intIndexEnd + 1;
                            }
                        }

                        intIndexEnd += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in SplitCommandLineParams", ex);
            }

            return strParameters.ToArray();
        }
    }
}
