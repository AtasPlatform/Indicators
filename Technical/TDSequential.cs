namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

using ATAS.Indicators.Drawing;

using MoreLinq;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;

[DisplayName("TD Sequential")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.TDSequentialDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000619193")]
public class TDSequential : Indicator
{
	#region Static and constants

	private const int _barsNum = 4;
	private const int _maxSignalNum = 9;

	#endregion

	#region Fields

	private readonly PaintbarsDataSeries _colorBars = new("ColorBars", Strings.Candles) { IsHidden = true };
	private readonly ValueDataSeries _down = new("Down", Strings.Down) { ShowZeroValue = false, VisualType = VisualMode.UpArrow };

	private readonly ValueDataSeries _res = new("Res", Strings.ResistanceLevel)
	{
		ShowZeroValue = false,
		Width = 2,
		VisualType = VisualMode.Line,
		LineDashStyle = LineDashStyle.Dot,
		Color = Colors.Green
	};

	private readonly ValueDataSeries _sup = new("Sup", Strings.SupportLevel)
	{
		ShowZeroValue = false,
		Width = 2,
		VisualType = VisualMode.Line,
		LineDashStyle = LineDashStyle.Dot,
		Color = Colors.Red
	};

	private readonly ValueDataSeries _td = new("TdId", "TD") { ShowZeroValue = false, IsHidden = true };
	private readonly ValueDataSeries _ts = new("TsId", "TS") { ShowZeroValue = false, IsHidden = true };
	private readonly ValueDataSeries _up = new("Up", Strings.Up) { ShowZeroValue = false, VisualType = VisualMode.DownArrow };
	
	private Color _buyBarsColor = DefaultColors.Green.Convert();
	private Color _buyOvershoot = Color.FromRgb(214, 255, 92);
	private Color _buyOvershoot1 = Color.FromRgb(209, 255, 71);
	private Color _buyOvershoot2 = Color.FromRgb(184, 230, 46);
	private Color _buyOvershoot3 = Color.FromRgb(143, 178, 36);
	private Color _sellBarsColor = DefaultColors.Red.Convert();
	private Color _sellOvershoot = Color.FromRgb(255, 102, 163);
	private Color _sellOvershoot1 = Color.FromRgb(255, 51, 133);
	private Color _sellOvershoot2 = Color.FromRgb(255, 0, 102);
	private Color _sellOvershoot3 = Color.FromRgb(204, 0, 82);

	private bool _isBarColor = true;
	private bool _isNumbers = true;
	private bool _isSr = true;
	private int _labelTextSize = 10;

    #endregion

    #region Visualization

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSignalNumbers), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.IsNeedShowLabelDescription))]
    public bool IsNumbers
    {
        get => _isNumbers;
        set
        {
            _isNumbers = value;
            _up.VisualType = value ? VisualMode.DownArrow : VisualMode.Hide;
            _down.VisualType = value ? VisualMode.UpArrow : VisualMode.Hide;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SRLevels), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ShowSupResLevelsDescription))]
    public bool IsSr
    {
        get => _isSr;
        set
        {
            _isSr = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighlightSignalBars), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.HighlightSignalBarsDescription))]
    public bool IsBarColor
    {
        get => _isBarColor;
        set
        {
            _isBarColor = value;
            RecalculateValues();
        }
    }

    [Range(7, 30)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.TextSizeDescription))]
    public int LabelTextSize
    {
        get => _labelTextSize;
        set
        {
            _labelTextSize = value;

            foreach (var label in Labels)
            {
                var textSize = GetTextSize(label.Value.Text);
                label.Value.FontSize = textSize;

                var condition = label.Value.Tag.Contains('+');
                var width = condition ? _up.Width : _down.Width;
                var offsetY = condition ? GetLabelOffsetY(true, (int)textSize) * width : GetLabelOffsetY(false, (int)textSize) * width;
                label.Value.YOffset = offsetY;
            }
        }
    }

    #endregion

    #region Candles

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Candles), Description = nameof(Strings.BuySignalColorDescription))]
    public Color BuyBarsColor
    {
        get => _buyBarsColor;
        set
        {
            _buyBarsColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyOvershootColor), GroupName = nameof(Strings.Candles), Description = nameof(Strings.BuySignalColorDescription))]
    public Color BuyOvershoot
    {
        get => _buyOvershoot;
        set
        {
            _buyOvershoot = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyOvershoot1Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.BuySignalColorDescription))]
    public Color BuyOvershoot1
    {
        get => _buyOvershoot1;
        set
        {
            _buyOvershoot1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyOvershoot2Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.BuySignalColorDescription))]
    public Color BuyOvershoot2
    {
        get => _buyOvershoot2;
        set
        {
            _buyOvershoot2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyOvershoot3Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.BuySignalColorDescription))]
    public Color BuyOvershoot3
    {
        get => _buyOvershoot3;
        set
        {
            _buyOvershoot3 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Candles), Description = nameof(Strings.SellSignalColorDescription))]
    public Color SellBarsColor
    {
        get => _sellBarsColor;
        set
        {
            _sellBarsColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellOvershootColor), GroupName = nameof(Strings.Candles), Description = nameof(Strings.SellSignalColorDescription))]
    public Color SellOvershoot
    {
        get => _sellOvershoot;
        set
        {
            _sellOvershoot = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellOvershoot1Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.SellSignalColorDescription))]
    public Color SellOvershoot1
    {
        get => _sellOvershoot1;
        set
        {
            _sellOvershoot1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellOvershoot2Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.SellSignalColorDescription))]
    public Color SellOvershoot2
    {
        get => _sellOvershoot2;
        set
        {
            _sellOvershoot2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellOvershoot3Color), GroupName = nameof(Strings.Candles), Description = nameof(Strings.SellSignalColorDescription))]
    public Color SellOvershoot3
    {
        get => _sellOvershoot3;
        set
        {
            _sellOvershoot3 = value;
            RecalculateValues();
        }
    }

    #endregion

    #region ctor

    public TDSequential()
		: base(true)
	{
		DenyToChangePanel = true;
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
		_up.Color = _buyBarsColor;
		_down.Color = _sellBarsColor;
		DataSeries.Add(_colorBars);
		DataSeries.Add(_td);
		DataSeries.Add(_ts);
		DataSeries.Add(_up);
		DataSeries.Add(_down);
		DataSeries.Add(_sup);
		DataSeries.Add(_res);

		_up.PropertyChanged += PropColorChanged;
		_down.PropertyChanged += PropColorChanged;
	}

	#endregion

	#region Protected Methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar < _barsNum)
			return;

		NumbersCalc(bar);
	}

	#endregion

	#region Private Methods

	#region Event Handlers

	private void PropColorChanged(object sender, PropertyChangedEventArgs e)
	{
		if ((ValueDataSeries)sender == _up)
		{
			var upLabels = Labels.Where(l => l.Value.Tag.Contains('+'));

			switch (e.PropertyName)
			{
				case "Color":
					var color = ((ValueDataSeries)sender).Color;
					upLabels.ForEach(l => l.Value.Textcolor = color.Convert());
					break;
				case "Width":
					var width = ((ValueDataSeries)sender).Width;

                    foreach (var upLabel in upLabels)
                    {
                        var textSize = GetTextSize(upLabel.Value.Text);
                        var offsetY = GetLabelOffsetY(true, (int)textSize) * width;
                        upLabel.Value.YOffset = offsetY;
                    }
                    break;
			}
		}

		if ((ValueDataSeries)sender == _down)
		{
			var upLabels = Labels.Where(l => !l.Value.Tag.Contains('+'));

			switch (e.PropertyName)
			{
				case "Color":
					var color = ((ValueDataSeries)sender).Color;
					upLabels.ForEach(l => l.Value.Textcolor = color.Convert());
					break;
				case "Width":
					var width = ((ValueDataSeries)sender).Width;
					
					foreach (var upLabel in upLabels)
					{
                        var textSize = GetTextSize(upLabel.Value.Text);
                        var offsetY = GetLabelOffsetY(false, (int)textSize) * width;
                        upLabel.Value.YOffset = offsetY;
                    }
					break;
			}
		}
	}

	#endregion

	#region Numbers

	private void NumbersCalc(int bar)
	{
		var curCandle = GetCandle(bar);
		var candle = GetCandle(bar - _barsNum);

		if (curCandle.Close > candle.Close)
			_td[bar] = _td[bar - 1] + 1;
		else
			_td[bar] = 0;

		if (curCandle.Close < candle.Close)
			_ts[bar] = _ts[bar - 1] + 1;
		else
			_ts[bar] = 0;

		var tdUp = _td[bar] - GetValueCurrentSmallerPrev(bar, _td, 2);
		var tdDown = _ts[bar] - GetValueCurrentSmallerPrev(bar, _ts, 2);

		SetSignal(bar, curCandle, tdUp, _up, _down, true);
		SetSignal(bar, curCandle, tdDown, _down, _up, false);

		if (_isBarColor)
		{
			SetBarsColor(tdUp, bar, _sellBarsColor, _sellOvershoot, _sellOvershoot1, _sellOvershoot2, _sellOvershoot3);
			SetBarsColor(tdDown, bar, _buyBarsColor, _buyOvershoot, _buyOvershoot1, _buyOvershoot2, _buyOvershoot3);
		}

		if (!_isSr)
			return;

		if (tdUp == _maxSignalNum)
		{
			_res.SetPointOfEndLine(bar - 1);
			_res[bar] = curCandle.High;
		}
		else
			_res[bar] = _res[bar - 1];

		if (tdDown == _maxSignalNum)
		{
			_sup.SetPointOfEndLine(bar - 1);
			_sup[bar] = curCandle.Low;
		}
		else
			_sup[bar] = _sup[bar - 1];
	}

	private void SetBarsColor(decimal td, int bar, Color color9, Color color13, Color color14, Color color15, Color color16)
	{
        _colorBars[bar] = td switch
        {
            9 => color9,
            13 => color13,
            14 => color14,
            15 => color15,
            16 => color16,
            _ => _colorBars[bar]
        };
    }

	private void SetSignal(int bar, IndicatorCandle candle, decimal tdValue, ValueDataSeries series, ValueDataSeries altSeries, bool isUp)
	{
		if (tdValue is < 1 or > _maxSignalNum)
			return;

		var markerPlace = isUp ? candle.High + InstrumentInfo.TickSize : candle.Low - InstrumentInfo.TickSize;
		series[bar] = markerPlace;
		altSeries[bar] = 0;

		if (_isNumbers)
		{
			var color = isUp ? _up.Color.Convert() : _down.Color.Convert();
			var tag = isUp ? $"{bar}+" : $"{bar}";
			var textSize = GetTextSize(tdValue);
			var offsetY = GetLabelOffsetY(isUp, (int)textSize) * series.Width;
			
			AddText(tag, tdValue.ToString(CultureInfo.InvariantCulture), true, bar, markerPlace, offsetY, 0,
				 color, System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, textSize,
				 DrawingText.TextAlign.Center);
		}
	}

    private float GetTextSize(decimal tdValue)
    {
        return tdValue == _maxSignalNum ? _labelTextSize * 1.5f : _labelTextSize;
    }

    private float GetTextSize(string tdValue)
    {
		return GetTextSize(decimal.Parse(tdValue));
    }

    private int GetLabelOffsetY(bool isUp, int textSize)
	{
		return isUp ? -textSize * 2 : textSize * 3;
	}

	private decimal GetValueCurrentSmallerPrev(int bar, ValueDataSeries series, int amount)
	{
		var count = 0;

		for (var i = bar; i > 0; i--)
		{
			if (series[i] < series[i - 1])
			{
				count++;

				if (count == amount)
					return series[i];
			}
		}

		return series[0];
	}

	#endregion

	#endregion
}