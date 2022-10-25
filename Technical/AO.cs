namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using OFT.Attributes;

[DisplayName("Awesome Oscillator")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/16995-awesome-oscillator")]
public class AwesomeOscillator : Indicator
{
	#region Fields

	private readonly ValueDataSeries _negative = new("Negative")
	{
		VisualType = VisualMode.Histogram,
		Color = Colors.Red,
		ShowZeroValue = false,
		IsHidden = true
	};

	private readonly ValueDataSeries _neutral = new("Neutral")
	{
		VisualType = VisualMode.Histogram,
		Color = Colors.Gray,
		ShowZeroValue = false,
		IsHidden = true
	};

	private readonly ValueDataSeries _positive = new("Positive")
	{
		VisualType = VisualMode.Histogram,
		Color = Colors.Green,
		ShowZeroValue = false,
		IsHidden = true
	};

	private int _p1 = 34;
	private int _p2 = 5;

	#endregion

	#region Properties

	[Range(1, 10000)]
	public int P1
	{
		get => _p1;
		set
		{
			if (value <= _p2)
				return;

            _p1 = value;
			RecalculateValues();
		}
	}

	[Range(1, 10000)]
    public int P2
	{
		get => _p2;
		set
		{
			if(value >= _p1)
				return;

			_p2 = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public AwesomeOscillator()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _positive;
		DataSeries.Add(_negative);
		DataSeries.Add(_neutral);
	}

	#endregion

	#region Public methods

	public override string ToString()
	{
		return "Awesome Oscillator";
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
			DataSeries.ForEach(x => x.Clear());

		if (bar <= _p1)
			return;

		decimal sma1 = 0;
		decimal sma2 = 0;

		for (var ct = 1; ct <= _p1; ct += 1)
		{
			var candleCt = GetCandle(bar - ct + 1);
			var midPrice = (candleCt.High + candleCt.Low) / 2;
			sma1 += midPrice;

			if (ct <= _p2)
				sma2 += midPrice;
		}

		var aw = sma2 / _p2 - sma1 / _p1;
		var lastAw = 0.0m;

		if (bar > 0)
		{
			if (_positive[bar - 1] != 0)
				lastAw = _positive[bar - 1];
			else if (_negative[bar - 1] != 0)
				lastAw = _negative[bar - 1];
		}

		if (aw > lastAw)
			_positive[bar] = aw;
		else if (aw < lastAw)
			_negative[bar] = aw;
		else
			_neutral[bar] = aw;

		this[bar] = aw;
	}

	#endregion
}