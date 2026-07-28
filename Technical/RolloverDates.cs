namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Threading;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Settings;

using Utils.Common.Logging;

[DisplayName("Rollover Dates")]
public class RolloverDates : Indicator
{
    #region Fields

    private SortedDictionary<int, ContractRollover> _barContracts = [];

    private int _lastBar = -1;
    private int _loading;
    private bool _isInitialized;
    private int _barUnderMouse;
    private Point _lastMousePosition;

    private FilterEnum<ContractRolloverType> _rolloverType;

    #endregion

    #region Properties

    #region Settings

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RolloverType), GroupName = nameof(Strings.Settings))]
    [Parameter]
    public FilterEnum<ContractRolloverType> RolloverType 
    { 
        get => _rolloverType;
        set => SetTrackedProperty(ref _rolloverType, value, (name) =>
        {
	        if (name == nameof(RolloverType.Value))
		        RefreshContractRolloversAsync();
        });
    }

    #endregion

    #region Drawing

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineSettings), GroupName = nameof(Strings.Drawing),
       Description = nameof(Strings.LineSettingsDescription))]
    public PenSettings LineSettings { get; set; }

    #endregion

    #region Labels

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLabelsOfFinishedLines), GroupName = nameof(Strings.Label),
     Description = nameof(Strings.ShowLabelsOfFinishedLinesDescription))]
    public bool ShowLabels { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Label),
       Description = nameof(Strings.FontSettingDescription))]
    public FontSetting FontSetting { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetX), GroupName = nameof(Strings.Label),
    Description = nameof(Strings.LabelOffsetXDescription))]
    public int LabelOffsetX { get; set; } = 5;

    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY), GroupName = nameof(Strings.Label),
    Description = nameof(Strings.LabelOffsetYDescription))]
    public int LabelOffsetY { get; set; } = 5;

    #endregion

    #endregion

    #region ctor

    public RolloverDates() : base(true)
    {
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        DenyToChangePanel = true;

        var data = DataSeries[0] as ValueDataSeries;
        data!.IsHidden = true;
        data!.ShowZeroValue = false;

        FontSetting = new("Segoe UI", 9);
        LineSettings = new()
        {
            Color = CrossColors.Red,
            Width = 2,
            LineDashStyle = LineDashStyle.Dash
        };

        RolloverType = new FilterEnum<ContractRolloverType>(false)
        {
	        Enabled = true,
            Value = ContractRolloverType.VolumeBasedCurrentEnd,
        };
		RolloverType.PropertyChanged += OnRolloverTypePropertyChanged;
    }

	#endregion

	#region Protected methods

	protected override void OnInitialize()
    {
        _isInitialized = true;
        RefreshContractRolloversAsync();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == _lastBar) 
            return;

        _lastBar = bar;

        if (IsNewSession(bar) && bar == CurrentBar - 1)
	        RefreshContractRolloversAsync();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null)
            return;

        DrawRollovers(context);
    }

    public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
    {
        _barUnderMouse = ChartInfo.MouseLocationInfo.BarBelowMouse;
        _lastMousePosition = ChartInfo.MouseLocationInfo.LastPosition;

        return base.ProcessMouseMove(e);
    }

    #endregion

    #region Private methods

    private async void RefreshContractRolloversAsync()
    {
		try
	    {
		    if (!_isInitialized || Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
			    return;

			RolloverType.SetEnabled(false);

		    var rollovers = await DataProvider?
			    .OnlineDataProvider?
			    .GetContractRolloversAsync(GetCandle(0).Time, GetCandle(CurrentBar - 1).LastTime, RolloverType.Value);

		    if (rollovers is null || rollovers.Rollovers.Length == 0)
			    return;

		    var barContracts = new SortedDictionary<int, ContractRollover>();

		    var index = 0;
		    var prevLineBar = 0;
		    var lastLineBar = 0;

		    for (var bar = 0; bar < CurrentBar; bar++)
		    {
			    if (index >= rollovers.Rollovers.Length)
				    break;

			    var candle = GetCandle(bar);
			    var time1 = candle.LastTime;
			    var time2 = bar == CurrentBar - 1 ? candle.LastTime : GetCandle(bar + 1).Time;
			    var rollover = rollovers.Rollovers[index];

			    if (rollover.Date > time1 && rollover.Date >= time2)
				    continue;

			    barContracts[bar] = rollover;
			    prevLineBar = lastLineBar;
				lastLineBar = bar;
				index++;
		    }

		    if (lastLineBar > prevLineBar && index < rollovers.Rollovers.Length)
			    barContracts[lastLineBar + lastLineBar - prevLineBar] = rollovers.Rollovers[index];

			_barContracts = barContracts;

		    RedrawChart();
		}
	    catch (Exception excp)
	    {
		    this.LogError("Failed to get contract expirations.", excp);
	    }
	    finally
	    {
		    Interlocked.Exchange(ref _loading, 0);
		    DoActionInGuiThread(() => RolloverType.SetEnabled(true));
	    }
    }

    private void DrawRollovers(RenderContext context)
    {
        if (_barContracts.Count is 0)
            return;

        for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
        {
	        if (!CheckBar(bar) || !_barContracts.TryGetValue(bar, out var item))
                continue;

	        DrawRollover(context, bar, item);
        }

        if (CurrentBar - 1 >= FirstVisibleBarNumber && CurrentBar - 1 <= LastVisibleBarNumber)
        {
	        var rollovers = _barContracts.Where(p => p.Key > CurrentBar);

	        foreach (var (b, r) in rollovers)
				DrawRollover(context, b, r);
		}
    }

    private void DrawRollover(RenderContext context, int bar, ContractRollover item)
    {
	    var x = ChartInfo.GetXByBar(bar, false);
	    var top = ChartInfo.Region.Top;
	    var bottom = ChartInfo.Region.Bottom;

	    context.DrawLine(LineSettings.RenderObject, x, top, x, bottom);

	    if (!ShowLabels)
		    return;

	    string text;

	    if (ToDrawTimeLabel(bar, x))
	    {
		    var time = item.Date.Add(InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero);
		    text = $"{item.Code} ({time:dd.MM.yyyy HH:mm})";
	    }
	    else
		    text = item.Code;

	    var xPosition = x;
	    var yPosition = top + LabelOffsetY;

	    if (RolloverType.Value is ContractRolloverType.ExpirationDate or ContractRolloverType.VolumeBasedCurrentEnd)
	    {
		    var textSize = context.MeasureString(text, FontSetting.RenderObject);

		    xPosition -= textSize.Width;
		    xPosition -= LabelOffsetX;
	    }
	    else
		    xPosition += LabelOffsetX;

	    context.DrawString(text, FontSetting.RenderObject, LineSettings.Color.Convert(), xPosition, yPosition);
    }

    private bool ToDrawTimeLabel(int bar, int x)
    {
        if (ChartInfo?.PriceChartContainer.BarsWidth > 5)
            return bar == _barUnderMouse;

        var shift = 5;
        return _lastMousePosition.X >= x - shift && _lastMousePosition.X <= x + shift;
    }

    private bool CheckBar(int bar)
    {
        return bar >= 0 && bar < CurrentBar;
	}

    private void OnRolloverTypePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
	    if (e.PropertyName == nameof(FilterEnum<ContractRolloverType>.Value))
		    RaisePanelPropertyChanged(Name);
    }

	#endregion
}
