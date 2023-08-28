namespace TSW.Indicators.Custom;

using ATAS.Indicators;

public class MyCustomIndicator : Indicator
{
    public MyCustomIndicator() : base(true)
    {
        DataSeries.Add(new ValueDataSeries("Values"));
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var period = 10;
        var start = Math.Max(0, bar - period + 1);
        var count = Math.Min(bar + 1, period);
        var max = (decimal)SourceDataSeries[start];
        for (var i = start + 1; i < start + count; i++)
        {
            max = Math.Max(max, (decimal)SourceDataSeries[i]);
        }
        this[bar] = max;
    }
}
