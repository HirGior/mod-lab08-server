using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScottPlot;

using System.Drawing;

class Request
{
    public int Id { get; set; }
}

class Server
{
    private readonly bool[] channels;
    private readonly object locker = new object();

    public int TotalRequests { get; private set; }
    public int RejectedRequests { get; private set; }
    public int ServedRequests { get; private set; }

    public int BusyChannels
    {
        get
        {
            lock (locker)
            {
                return channels.Count(c => c);
            }
        }
    }

    private readonly int serviceTime;

    public Server(int channelCount, int serviceTime)
    {
        channels = new bool[channelCount];
        this.serviceTime = serviceTime;
    }

    public void HandleRequest(Request request)
    {
        TotalRequests++;

        lock (locker)
        {
            for (int i = 0; i < channels.Length; i++)
            {
                if (!channels[i])
                {
                    channels[i] = true;

                    Task.Run(() =>
                    {
                        Thread.Sleep(serviceTime);

                        lock (locker)
                        {
                            channels[i] = false;
                            ServedRequests++;
                        }
                    });

                    return;
                }
            }
        }

        RejectedRequests++;
    }
}

class Client
{
    private readonly Server server;
    private readonly int requestInterval;

    public Client(Server server, int requestInterval)
    {
        this.server = server;
        this.requestInterval = requestInterval;
    }

    public void Start(int requestCount)
    {
        for (int i = 0; i < requestCount; i++)
        {
            Request request = new Request
            {
                Id = i + 1
            };

            server.HandleRequest(request);

            Thread.Sleep(requestInterval);
        }
    }
}

class ExperimentResult
{
    public double Lambda { get; set; }

    public double PIdle { get; set; }

    public double PReject { get; set; }

    public double Q { get; set; }

    public double A { get; set; }

    public double Busy { get; set; }
}

class Program
{

    static void SavePlot(
        List<ExperimentResult> results,
        Func<ExperimentResult, double> selector,
        string title,
        string fileName)
    {
        double[] xs = results
            .Select(r => r.Lambda)
            .ToArray();

        double[] ys = results
            .Select(selector)
            .ToArray();

        var plot = new ScottPlot.Plot();

        plot.Add.Scatter(xs, ys);

        plot.Title(title);

        plot.XLabel("Lambda interval");

        plot.YLabel(title);

        Directory.CreateDirectory("result");

        plot.SavePng($"result/{fileName}", 800, 600);
    }

    static void Main()
    {
        int channels = 3;

        int serviceTime = 1000;

        List<string> report = new List<string>();

        report.Add("SMO MODEL RESULTS");
        report.Add("");

        List<ExperimentResult> results = new();

        for (int requestInterval = 100; requestInterval <= 1000;
             requestInterval += 100)
        {
            Server server = new Server(
                channels,
                serviceTime
            );

            Client client = new Client(
                server,
                requestInterval
            );

            int requests = 100;

            client.Start(requests);

            Thread.Sleep(5000);

            double refusalProbability =
                (double)server.RejectedRequests /
                server.TotalRequests;

            double throughput =
                (double)server.ServedRequests /
                server.TotalRequests;

            double absoluteThroughput =
                server.ServedRequests / 10.0;

            double averageBusy =
                throughput * channels;

            double idleProbability =
                1.0 - averageBusy / channels;

            string line =
                $"Lambda interval: {requestInterval} ms | " +
                $"Total: {server.TotalRequests} | " +
                $"Served: {server.ServedRequests} | " +
                $"Rejected: {server.RejectedRequests} | " +
                $"P_idle: {idleProbability:F3} | " +
                $"P_reject: {refusalProbability:F3} | " +
                $"Q: {throughput:F3} | " +
                $"A: {absoluteThroughput:F3} | " +
                $"Busy: {averageBusy:F3}";

            Console.WriteLine(line);

            report.Add(line);

            results.Add(new ExperimentResult
            {
                Lambda = requestInterval,
                PIdle = idleProbability,
                PReject = refusalProbability,
                Q = throughput,
                A = absoluteThroughput,
                Busy = averageBusy
            });
        }

        SavePlot(results, r => r.PIdle,
            "Probability of idle",
            "p-1.png");

        SavePlot(results, r => r.PReject,
            "Probability of reject",
            "p-2.png");

        SavePlot(results, r => r.Q,
            "Relative throughput",
            "p-3.png");

        SavePlot(results, r => r.A,
            "Absolute throughput",
            "p-4.png");

        SavePlot(results, r => r.Busy,
            "Busy channels",
            "p-5.png");

        File.WriteAllLines(
            "result/results.txt",
            report
        );

        Console.WriteLine();
        Console.WriteLine("Results saved.");
    }
}