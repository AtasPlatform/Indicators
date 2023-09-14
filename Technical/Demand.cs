namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Demand Index")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45452-demand-index")]
public class Demand : Indicator
{
	#region Fields

	private readonly EMA _emaBp = new() { Period = 10 };
	private readonly EMA _emaRange = new() { Period = 10 };
	private readonly EMA _emaSp = new() { Period = 10 };
	private readonly EMA _emaVolume = new() { Period = 10 };

	private readonly ValueDataSeries _priceSumSeries = new("PriceSum");
	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Indicator);
    private readonly ValueDataSeries _smaSeries = new("SmaSeries", Strings.SMA)
    {
        Color = DefaultColors.Blue.Convert(),
        IgnoredByAlerts = true
    };

    private readonly SMA _sma = new()
	{
		Period = 10
	};

    #endregion

    #region Properties

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuySellPower), GroupName = nameof(Strings.Period), Order = 100)]
	[Range(1, 10000)]
	public int BuySellPower
	{
		get => _emaRange.Period;
		set
		{
			_emaRange.Period = _emaVolume.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuySellPower), GroupName = nameof(Strings.Smooth), Order = 200)]
	[Range(1, 10000)]
	public int BuySellSmooth
	{
		get => _emaBp.Period;
		set
		{
			_emaBp.Period = _emaSp.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Indicator), GroupName = nameof(Strings.Smooth), Order = 210)]
	[Range(1, 10000)]
	public int IndicatorSmooth
	{
		get => _sma.Period;
		set
		{
			_sma.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public Demand()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		LineSeries.Add(new LineSeries("ZeroVal", Strings.ZeroValue) { Color = Colors.Gray, Value = 0 });

		DataSeries[0] = _renderSeries;
		DataSeries.Add(_smaSeries);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);
		_priceSumSeries[bar] = candle.High + candle.Low + 2 * candle.Close;
		_emaVolume.Calculate(bar, candle.Volume);

		if (bar == 0)
		{
			_sma.Calculate(bar, 0);
			return;
		}

		var firstCandle = GetCandle(0);

		var bp = 0m;

		if (_emaVolume[bar] != 0 && firstCandle.High != firstCandle.Low && _priceSumSeries[bar] != 0)
		{
			if (_priceSumSeries[bar] < _priceSumSeries[bar - 1])
			{
				bp = candle.Volume / _emaVolume[bar] /
					(decimal)Math.Exp(0.375 * (double)(
						(_priceSumSeries[bar] + _priceSumSeries[bar - 1]) / (firstCandle.High - firstCandle.Low) *
						(_priceSumSeries[bar - 1] - _priceSumSeries[bar]) / _priceSumSeries[bar]
					));
			}
			else
				bp = candle.Volume / _emaVolume[bar];
		}
		else
			bp = candle.Volume / _emaVolume[bar - 1];

		var sp = 0m;

		if (_emaVolume[bar] != 0 && firstCandle.High != firstCandle.Low && _priceSumSeries[bar - 1] != 0)
		{
			if (_priceSumSeries[bar] <= _priceSumSeries[bar - 1])
				sp = candle.Volume / _emaVolume[bar];
			else
			{
				sp = candle.Volume / _emaVolume[bar] /
					(decimal)Math.Exp(0.375 * (double)(
						(_priceSumSeries[bar] + _priceSumSeries[bar - 1]) / (firstCandle.High - firstCandle.Low) *
						(_priceSumSeries[bar] - _priceSumSeries[bar - 1]) / _priceSumSeries[bar - 1]
					));
			}
		}
		else
			sp = candle.Volume / _emaVolume[bar - 1];

		_emaBp.Calculate(bar, bp);
		_emaSp.Calculate(bar, sp);

		var q = 0m;

		if (_emaBp[bar] > _emaSp[bar])
			q = _emaBp[bar] == 0 ? 0 : _emaSp[bar] / _emaBp[bar];
		else if (_emaBp[bar] < _emaSp[bar])
			q = _emaSp[bar] == 0 ? 0 : _emaBp[bar] / _emaSp[bar];
		else
			q = 1;

		var di = 0m;

		if (_emaSp[bar] <= _emaBp[bar])
			di = 100 * (1 - q);
		else
			di = 100 * (q - 1);

		_renderSeries[bar] = di;
		_smaSeries[bar] = _sma.Calculate(bar, di);
	}

	#endregion
}