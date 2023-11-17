namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Bill Williams AC")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ACDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602333")]
public class ACBW : Indicator
{
	#region Fields

	private readonly SMA _longSma = new();

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

	private readonly SMA _shortSma = new();
	private readonly SMA _signalSma = new();

	private Color _negColor = DefaultColors.Purple;
	private Color _neutralColor = DefaultColors.Gray;
	private Color _posColor = DefaultColors.Green;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueDescription), Order = 610)]
	public System.Windows.Media.Color PosColor
	{
		get => _posColor.Convert();
		set
		{
			_posColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueDescription), Order = 620)]
	public System.Windows.Media.Color NegColor
	{
		get => _negColor.Convert();
		set
		{
			_negColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Neutral), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NeutralValueDescription), Order = 630)]
	public System.Windows.Media.Color NeutralColor
	{
		get => _neutralColor.Convert();
		set
		{
			_neutralColor = value.Convert();
			RecalculateValues();
		}
	}

	[Parameter]
	[Range(1, 10000)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.LongPeriodDescription), Order = 100)]
	public int LongPeriod
	{
		get => _longSma.Period;
		set
		{
			_longSma.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodDescription), Order = 110)]
	public int ShortPeriod
	{
		get => _shortSma.Period;
		set
		{
			_shortSma.Period = value;

			RecalculateValues();
		}
	}

    [Parameter]
    [Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SignalPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SignalPeriodDescription), Order = 120)]
	public int SignalPeriod
	{
		get => _signalSma.Period;
		set
		{
			_signalSma.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ACBW()
	{
		Panel = IndicatorDataProvider.NewPanel;

		_shortSma.Period = _signalSma.Period = 50;
		_longSma.Period = 51;

		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var diff = _shortSma.Calculate(bar, value) - _longSma.Calculate(bar, value);
		var ac = diff - _signalSma.Calculate(bar, diff);
		_renderSeries[bar] = ac;

		if (bar == 0)
		{
			DataSeries.ForEach(x => x.Clear());
			return;
		}

		var prevValue = _renderSeries[bar - 1];

		if (ac > prevValue)
			_renderSeries.Colors[bar] = _posColor;
		else if (ac < prevValue)
			_renderSeries.Colors[bar] = _negColor;
		else
			_renderSeries.Colors[bar] = _posColor;
	}

	#endregion
}