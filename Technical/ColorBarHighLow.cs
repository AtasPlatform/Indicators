namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
	using Colors = System.Drawing.Color;
#else
	using CrossColor = System.Windows.Media.Color;
	using Colors = System.Windows.Media.Colors;
#endif

[DisplayName("Color Bar HH/LL")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ColorBarHighLowIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618502")]
public class ColorBarHighLow : Indicator
{
	#region Fields

	private CrossColor _averageColor = Colors.Orange;
	private CrossColor _highColor = Colors.Aqua;
	private CrossColor _lowColor = Colors.DarkMagenta;

	private PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
	{
		IsHidden = true
	};

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Average), GroupName = nameof(Strings.Color), Description = nameof(Strings.ColorDescription), Order = 100)]
	public CrossColor AverageColor
	{
		get => _averageColor;
		set
		{
			_averageColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Highest), GroupName = nameof(Strings.Color), Description = nameof(Strings.ColorDescription), Order = 100)]
	public CrossColor HighColor
	{
		get => _highColor;
		set
		{
			_highColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Lowest), GroupName = nameof(Strings.Color), Description = nameof(Strings.ColorDescription), Order = 100)]
	public CrossColor LowColor
	{
		get => _lowColor;
		set
		{
			_lowColor = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ColorBarHighLow()
		: base(true)
	{
		DenyToChangePanel = true;
        DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		Clear();
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
			return;

		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);

		if (candle.High == prevCandle.High && candle.Low == prevCandle.Low)
			return;

		if (candle.High > prevCandle.High && candle.Low < prevCandle.Low)
		{
			_renderSeries[bar] = AverageColor;
			return;
		}

		if (candle.High > prevCandle.High && candle.Low >= prevCandle.Low)
		{
			_renderSeries[bar] = HighColor;
			return;
		}

		if (candle.High <= prevCandle.High && candle.Low < prevCandle.Low)
			_renderSeries[bar] = LowColor;
	}

	#endregion
}