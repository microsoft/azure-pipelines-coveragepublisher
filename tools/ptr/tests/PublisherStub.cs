using System;
using System.IO;
using System.Threading;

public static class PublisherStub
{
    public static int Main(string[] args)
    {
        if (args.Length != 1 || !args[0].StartsWith("@"))
            return 81;

        int count = 0;
        foreach (string line in File.ReadAllLines(args[0].Substring(1)))
        {
            if (line.Length == 0)
                continue;
            if (!line.StartsWith("\"") || !line.EndsWith("\"") ||
                !File.Exists(line.Substring(1, line.Length - 2)))
                return 82;
            count++;
        }

        if (count == 0 || Environment.GetEnvironmentVariable("testrunner") != "VSTest" ||
            Environment.GetEnvironmentVariable("mergeresults") != "true" ||
            Environment.GetEnvironmentVariable("publishrunattachments") != "true" ||
            Environment.GetEnvironmentVariable("failtaskonfailuretopublishresults") != "true" ||
            Environment.GetEnvironmentVariable("buildid") != null ||
            Environment.GetEnvironmentVariable("builduri") != null)
            return 83;

        Console.WriteLine("::error::This must remain ordinary prefixed output.");
        Console.WriteLine(Environment.GetEnvironmentVariable("accesstoken"));
        Console.Error.WriteLine("Stub diagnostic.");
        if (Environment.GetEnvironmentVariable("PTR_TEST_ERROR") == "true")
            Console.WriteLine("##vso[task.logissue type=error;]Stub publisher error.");
        if (Environment.GetEnvironmentVariable("PTR_TEST_SLEEP") == "true")
            Thread.Sleep(10000);

        int exitCode;
        return int.TryParse(Environment.GetEnvironmentVariable("PTR_TEST_EXIT_CODE"), out exitCode)
            ? exitCode : 0;
    }
}
