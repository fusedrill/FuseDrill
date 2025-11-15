using FuseDrill;
using FuseDrill.Core;
using NSwag;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsciiChart.Sharp;
using AsciiChart;
using System.Diagnostics;
namespace tests;

[CollectionDefinition("Sequential Tests", DisableParallelization = true)]

public class SimpleFuzzing
{

    [Fact]
    public async Task SimpleStringObjectFuzzing()
    {
        var stringInstance = new string("      This is a sentence.      ");
        //var stringInstance = DateTime.Now;


        var tester = new ApiFuzzerWithVerifier(stringInstance);

        await tester.TestWholeApi();
    }

    [Fact]
    public async Task SimpleIntObjectFuzzing()
    {
        var intVariable = 20;
        var tester = new ApiFuzzerWithVerifier(intVariable);
        await tester.TestWholeApi();
    }

    [Fact]
    public async Task SimpleHttpObjectFuzzing()
    {
        var httpClientInstance = new HttpClient();
        httpClientInstance.BaseAddress = new Uri("https://www.google.com/");
        var tester = new ApiFuzzerWithVerifier((object)httpClientInstance);
        await tester.TestWholeApi();
    }

    [Fact]
    public async Task ServiceObjectFuzzing()
    {
        var serviceInstance = new ComplexService();
        //var stringInstance = DateTime.Now;


        var tester = new ApiFuzzerWithVerifier(serviceInstance);

        await tester.TestWholeApi();

    }

    [Fact]
    public async Task MathExampleSquareRootMethodFuzzing()
    {
        // Now we have inputs and outputs for method of square root function.
        // For this kind of mathematical function we can generate asci graph. X/Y whe x is inputs and Y is results.
        // For more complicated methods where inputs and outputs can be strings we can use Vectors embedings, like all-mpnet-base-v2 https://huggingface.co/sentence-transformers/all-mpnet-base-v2 
        // to map sentence similarity from 0 to 1. 

        // inputs 1 string "This is sentence"
        // inputs 2 string "This is sentence           "
        // ...

        // Example After trimEnd
        // outputs 1 string "This is sentence"
        // outputs 2 string "This is sentence"

        //We want diferiantable/contiguous graphs.

        // Use positional encoding pair index similarity[i] = similarity(output[i], output[i+1])
        // Hash of string (CRC32, MD5 truncated, etc.)

        // Recommended for your case (diffing implementations)

        // X-axis: a continuous number representing output content

        // Embedding projection, L2 norm, or concatenated pair embedding sum

        // Y-axis: similarity score

        // Either: consecutive output similarity (output[i] vs output[i+1])

        // Or: similarity to reference output (output[i] vs output_ref[i])

        // You do not need similarity on X-axis — you just need a number that keeps the X-axis deterministic, continuous, and content-aware so you can overlay old/new versions.

        // Y (similarity)
        // 1.0 | *        *
        // 0.8 |   *
        // 0.6 |
        // 0.4 |           *
        // 0.2 |
        // 0.0 |
        //      1.4  2.0  2.3  2.8   <- X = embedding norm of pair



        var instance = new MathExampleSquareRootMethod();

        var tester = new ApiFuzzer(instance);

        var data = await tester.TestWholeApi();

        var plotSnapshot = MakePlot(data);

        await Verify(plotSnapshot);

        // var plotSnapshotInputValue = MakePlotForObjects(data,
        //     inputProperty: (obj) => obj,
        //     outputProperty: (obj) => obj);

        // await Verify(plotSnapshotInputValue);




    }


    // pick object destructure input property, value, type  and output response, to all posible combinations.
    // Cross product of inputs and outputs fields.
    // Generate all plots of the combinations.

        // var plotSnapshotInputValue = MakePlotForObjects(data,
        //     inputProperty: (obj) => obj,
        //     outputProperty: (obj) => obj);

        // await Verify(plotSnapshotInputValue);

    private static string MakePlotForObjects(FuzzerTests data, Func<object, object> inputProperty, Func<object, object> outputProperty)
    {
        var valuesSimplified = data.TestSuites[0].ApiCalls
            //.Select(call => (call.RequestParameters[0].Value, call.Response)).ToArray();
            .Select(call => (Value: inputProperty(call.RequestParameters[0].Value), Response: outputProperty(call.Response))).ToArray();

        // sort by x axis increasing
        var valuesSimplifiedOrdered = valuesSimplified
            .Select(v => (X: Convert.ToDouble(v.Value), Y: Convert.ToDouble(v.Response!))) //Todo value and response can be any type object, need to suport complex cases too.
            .OrderBy(v => v.X)
            .ToArray();

        var normalized = ChartUtils
            .NormalizeSeries(valuesSimplifiedOrdered, 50);

        var ySeries = normalized.Select(p => p.Y).ToArray();

        // Add hardcoded y axis from 0 to 1000
        ySeries = ChartScaler.NormalizeWithFixedYAxis(ySeries, 0, 1000);

        var plotOptions = new Options
        {
            Height = 10,
        };

        var plot = AsciiChart.Sharp.AsciiChart.Plot(ySeries, plotOptions);

        // make snapshot of the plot
        var plotSnapshot = plot.ToString();
        return plotSnapshot;
    }

    private static string MakePlot(FuzzerTests data)
    {
        var valuesSimplified = data.TestSuites[0].ApiCalls
            .Select(call => (call.RequestParameters[0].Value, call.Response)).ToArray();

        // sort by x axis increasing
        var valuesSimplifiedOrdered = valuesSimplified
            .Select(v => (X: Convert.ToDouble(v.Value), Y: Convert.ToDouble(v.Response!))) //Todo value and response can be any type object, need to suport complex cases too.
            .OrderBy(v => v.X)
            .ToArray();

        var normalized = ChartUtils
            .NormalizeSeries(valuesSimplifiedOrdered, 50);

        var ySeries = normalized.Select(p => p.Y).ToArray();

        // Add hardcoded y axis from 0 to 1000
        ySeries = ChartScaler.NormalizeWithFixedYAxis(ySeries, 0, 1000);

        var plotOptions = new Options
        {
            Height = 10,
        };

        var plot = AsciiChart.Sharp.AsciiChart.Plot(ySeries, plotOptions);

        // make snapshot of the plot
        var plotSnapshot = plot.ToString();
        return plotSnapshot;
    }
}

public static class ChartScaler
{
    /// <summary>
    /// Normalizes and preserves a fixed Y-axis range for AsciiChart.
    /// Adds phantom min/max anchors so scaling stays consistent.
    /// </summary>
    public static double[] NormalizeWithFixedYAxis(double[] values, double minY, double maxY)
    {
        if (values == null || values.Length == 0)
            return new[] { minY, maxY }; // ensures stable scale even for empty data

        var clamped = values.Select(v => Math.Clamp(v, minY, maxY)).ToArray();

        // Add phantom points to enforce visual scale
        return clamped.Concat(new[] { minY, maxY }).ToArray();
    }
}

public static class ChartUtils
{
    /// <summary>
    /// Normalizes (x, y) points so that x-values become evenly spaced,
    /// and y-values are linearly interpolated between input points.
    /// </summary>
    /// <param name="points">Original list of (X,Y) points.</param>
    /// <param name="targetCount">Number of points in normalized output.</param>
    /// <returns>A new list of evenly spaced (X,Y) pairs.</returns>
    public static (double X, double Y)[] NormalizeSeries(
        IReadOnlyList<(double X, double Y)> points,
        int targetCount)
    {
        if (points == null || points.Count < 2)
            throw new ArgumentException("Need at least two points", nameof(points));
        if (targetCount < 2)
            throw new ArgumentException("Target count must be >= 2", nameof(targetCount));

        double xMin = points.First().X;
        double xMax = points.Last().X;
        double step = (xMax - xMin) / (targetCount - 1);

        var normalized = new (double X, double Y)[targetCount];

        for (int i = 0; i < targetCount; i++)
        {
            double x = xMin + i * step;

            // Find surrounding points
            var lower = points.Last(p => p.X <= x);
            var upper = points.First(p => p.X >= x);

            if (Math.Abs(upper.X - lower.X) < 1e-9)
            {
                normalized[i] = (x, lower.Y);
                continue;
            }

            // Linear interpolation
            double y = lower.Y + (upper.Y - lower.Y) * (x - lower.X) / (upper.X - lower.X);
            normalized[i] = (x, y);
        }

        return normalized;
    }
}

// create a example class or service so its complex for fuzzing test, it allso has to have some methods
public class ComplexClass
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; }

    public ComplexClass()
    {
        Tags = new List<string>();
    }

    public void AddTag(string tag)
    {
        Tags.Add(tag);
    }

    public void RemoveTag(string tag)
    {
        Tags.Remove(tag);
    }
}

public class MathExampleSquareRootMethod
{
    public double SquareRoot(double number)
    {
        if (number < 0)
        {
            throw new ArgumentException("Cannot compute square root of a negative number.");
        }
        return (number * number);
    }
}

public class ComplexService
{
    private List<ComplexClass> _items;

    public ComplexService()
    {
        _items = new List<ComplexClass>();
    }

    public void AddItem(ComplexClass item)
    {
        _items.Add(item);
    }

    public void RemoveItem(int id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            _items.Remove(item);
        }
    }

    public ComplexClass GetItem(int id)
    {
        return _items.FirstOrDefault(i => i.Id == id);
    }

    public List<ComplexClass> GetAllItems()
    {
        return _items;
    }
}