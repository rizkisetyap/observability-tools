// ------------------------------------------------------------------------------------
// TimeSpanConverterTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Text.Json;

namespace ratings.Tests;


public class TimeSpanConverterTests
{
    [Fact]
    public void TimeSpanConverter_ShouldSerializeCorrectly()
    {
        var timespan = TimeSpan.FromMinutes(2);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeSpanConverter());

        var json = JsonSerializer.Serialize(timespan, options);
        Assert.Equal("\"00:02:00\"", json);
    }

    [Fact]
    public void TimeSpanConverter_ShouldReturnDefaultOnRead()
    {
        const string json = "\"00:02:00\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeSpanConverter());

        var result = JsonSerializer.Deserialize<TimeSpan>(json, options);
        Assert.Equal(TimeSpan.Zero, result);
    }
    
    [Fact]
    public void TimeSpanConverter_WritesExpectedString()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeSpanConverter());

        var json = JsonSerializer.Serialize(TimeSpan.FromMinutes(90), options);

        Assert.Equal("\"01:30:00\"", json);
    }

    [Fact]
    public void TimeSpanConverter_Read_ReturnsDefault()
    {
        var converter = new TimeSpanConverter();

        var reader = new Utf8JsonReader(
            "\"01:00:00\""u8,
            isFinalBlock: true,
            state: default
        );

        reader.Read();
        var result = converter.Read(ref reader, typeof(TimeSpan), new JsonSerializerOptions());

        Assert.Equal(TimeSpan.Zero, result);
    }
}