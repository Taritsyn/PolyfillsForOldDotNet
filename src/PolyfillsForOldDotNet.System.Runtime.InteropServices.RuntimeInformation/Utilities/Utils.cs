using System;
using System.Diagnostics;

using PolyfillsForOldDotNet.System.Runtime.InteropServices.Resources;

namespace PolyfillsForOldDotNet.System.Runtime.InteropServices.Utilities
{
	internal static class Utils
	{
		internal static string ReadProcessOutput(string fileName)
		{
			return ReadProcessOutput(fileName, string.Empty);
		}

		internal static string ReadProcessOutput(string fileName, string args)
		{
			if (string.IsNullOrWhiteSpace(fileName))
			{
				throw new ArgumentException(Strings.Argument_EmptyValue, nameof(fileName));
			}

			string output;
			var processInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = args ?? string.Empty,
				UseShellExecute = false,
				RedirectStandardOutput = true
			};

			using (Process process = Process.Start(processInfo))
			{
				output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
			}

			return output;
		}
	}
}