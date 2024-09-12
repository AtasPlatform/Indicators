namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;

[DisplayName("ADX")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ADXDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602313")]
public class ADX : Indicator
{
	#region Nested types

	private class Rma
	{
		#region Fields

		private decimal _alpha;
		private int _lastBar;
		private int _period;
		private decimal _prevSum;

		private decimal _sum;

		#endregion

		#region Properties

		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_alpha = 1m / value;
			}
		}

		#endregion

		#region ctor

		public Rma(int period)
		{
			Period = period;
		}

		public Rma()
		{
			Period = 14;
		}

		#endregion

		#region Public methods

		public decimal Calculate(int bar, decimal value)
		{
			if (bar is 0)
			{
				_sum = _prevSum = 0;
				return 0;
			}

			if (_lastBar != bar)
				_prevSum = _sum;

			_sum = _alpha * value + (1 - _alpha) * _prevSum;

			_lastBar = bar;
			return _sum;
		}

		#endregion
	}

	#endregion

	#region Fields

	private Rma _adxRma = new();
	private Rma _minusDmRma = new();
	private Rma _plusDmRma = new();
    private Rma _trRma = new();

    private ValueDataSeries _adxSeries = new("Adx", nameof(Strings.ADX))
	{
		Color = DefaultColors.Green.Convert(),
		DescriptionKey = nameof(Strings.ADX)
	};
	
	private ValueDataSeries _negSeries = new("DiNeg", nameof(Strings.DINeg))
	{
		Color = DefaultColors.Red.Convert(),
		DescriptionKey = nameof(Strings.DIMinusDescription),
		LineDashStyle = LineDashStyle.Dash
	};
	
	private ValueDataSeries _posSeries = new("DiPos", nameof(Strings.DIPos))
	{
		Color = DefaultColors.Blue.Convert(),
		DescriptionKey = nameof(Strings.DIPlusDescription)
	};
	
	#endregion

	#region Properties

	//ADX Smoothing
	[Parameter]
	[Range(1, 10000)]
	[Display(ResourceType = typeof(Strings),
		Name = nameof(Strings.Smoothing),
		GroupName = nameof(Strings.Common),
		Description = nameof(Strings.SmoothPeriodFilterDescription),
		Order = 19)]
	public int SmoothPeriod
	{
		get => _adxRma.Period;
		set
		{
			_adxRma.Period = value;
			RecalculateValues();
		}
	}

	//DI Length
	[Parameter]
	[Range(1, 10000)]
	[Display(ResourceType = typeof(Strings),
		Name = nameof(Strings.Period),
		GroupName = nameof(Strings.Common),
		Description = nameof(Strings.PeriodDescription),
		Order = 20)]
	public int Period
	{
		get => _trRma.Period;
		set
		{
			_trRma.Period = _plusDmRma.Period = _minusDmRma.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ADX()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _adxSeries;
		DataSeries.Add(_posSeries);
		DataSeries.Add(_negSeries);

		Period = SmoothPeriod = 14;
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		DataSeries.ForEach(x => x.Clear());
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		if (bar < Period)
		{
			DataSeries.ForEach(ds => ((ValueDataSeries)ds).SetPointOfEndLine(bar));

			if (bar is 0)
			{
				_trRma.Calculate(bar, 0);
				_plusDmRma.Calculate(bar, 0);
				_minusDmRma.Calculate(bar, 0);
				_adxRma.Calculate(bar, 0);
				return;
			}
		}

		var prevCandle = GetCandle(bar - 1);

		var up = candle.High - prevCandle.High;
		var down = prevCandle.Low - candle.Low;

		var plusDm = up > down && up > 0 ? up : 0;
		var minusDm = down > up && down > 0 ? down : 0;

		var tr = Math.Max(candle.High - candle.Low, Math.Abs(candle.High - prevCandle.Close));
		tr = Math.Max(tr, Math.Abs(candle.Low - prevCandle.Close));

		var trur = _trRma.Calculate(bar, tr);

		var plus = trur is 0 ? 0 : 100m * _plusDmRma.Calculate(bar, plusDm) / trur;
		var minus = trur is 0 ? 0 : 100m * _minusDmRma.Calculate(bar, minusDm) / trur;

		_posSeries[bar] = plus;
		_negSeries[bar] = minus;

		var sum = plus + minus;

		var adx = 100m * _adxRma.Calculate(bar, Math.Abs(plus - minus) / (sum == 0 ? 1 : sum));

		_adxSeries[bar] = adx;
	}

	#endregion
}