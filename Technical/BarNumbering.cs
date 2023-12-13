namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

[DisplayName("Bar Numbering")]
[Category("Other")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarNumberingDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618457")]
public class BarNumbering : Indicator
{
	#region Fields

	private RenderStringFormat _format = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center
	};

	private int _lastBar = -1;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.FontSettingDescription), Order = 90)]
	public FontSetting Font { get; set; } = new("arial", 10);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelTextColorDescription), Order = 95)]
	public Color FontColor { get; set; } = Color.Gray;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.DisplayBottom), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.DisplayLabelChartBottomDescription), Order = 100)]
	public bool DisplayBottom { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalOffset), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelOffsetYDescription), Order = 110)]
	public int Offset { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PeriodDescription), Order = 120)]
	[Range(1, 100000)]
	public int Period { get; set; } = 10;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResetOnSession), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ResetOnSessionFilterDescription), Order = 130)]
	public bool ResetOnSession { get; set; }

	#endregion

	#region ctor

	public BarNumbering()
		: base(true)
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical);
		DenyToChangePanel = true;

		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		DataSeries[0].IsHidden = true;
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    FontColor = ChartInfo.ColorsStore.AxisTextColor;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null || InstrumentInfo is null)
			return;

		for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
		{
			var barNum = ResetOnSession
				? this[bar] + 1
				: bar + 1;

			var renderString = barNum.ToString(CultureInfo.InvariantCulture);
			var stringSize = context.MeasureString(renderString, Font.RenderObject);

			if (barNum % Period != 0)
				continue;

			var x = ChartInfo.GetXByBar(bar, false);

			var y1 = Offset;

			if (DisplayBottom)
				y1 += Container.Region.Height - stringSize.Height;
			else
			{
				var low = GetCandle(bar).Low;
				y1 += ChartInfo.GetYByPrice(low - InstrumentInfo.TickSize, false);
			}
			
			context.DrawString(renderString, Font.RenderObject, FontColor, x, y1, _format);
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (_lastBar == bar)
			return;

		this[bar] = IsNewSession(bar)
			? 0
			: this[bar - 1] + 1;

		_lastBar = bar;
	}

	#endregion
}