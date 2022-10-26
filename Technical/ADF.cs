namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Accumulation / Distribution Flow")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43369-accumulation-distribution-flow")]
public class ADF : Indicator
{
	#region Fields

	private readonly ValueDataSeries _adf = new("AdfValues");

	private readonly ValueDataSeries _renderSeries = new("ADF")
	{
		VisualType = VisualMode.Histogram,
		UseMinimizedModeIfEnabled = true
	};

	private readonly SMA _sma = new()
	{
		Period = 14
	};

	private bool _usePrev = true;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 100)]
	[Range(1, 10000)]
	public int Period
	{
		get => _sma.Period;
		set
		{
			_sma.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UsePreviousClose", GroupName = "Settings", Order = 110)]
	public bool UsePrev
	{
		get => _usePrev;
		set
		{
			_usePrev = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ADF()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_sma.Calculate(bar, _adf[bar]);
			return;
		}

		var candle = GetCandle(bar);

		if (candle.High - candle.Low == 0)
			_adf[bar] = _adf[bar - 1];
		else
		{
			if (_usePrev)
			{
				var prevCandle = GetCandle(bar - 1);
				_adf[bar] = _adf[bar - 1] + (candle.Close - prevCandle.Close) * candle.Volume / (candle.High - candle.Low);
			}
			else
				_adf[bar] = _adf[bar - 1] + (candle.Close - candle.Open) * candle.Volume / (candle.High - candle.Low);
		}

		_renderSeries[bar] = _sma.Calculate(bar, _adf[bar]);
	}

	#endregion
}