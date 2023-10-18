namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Drawing;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using Color = System.Drawing.Color;

[DisplayName("Candle Statistics")]
public class CandleStatistics : Indicator
{
    #region Nested types

    public enum LabelLocations
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AboveCandle))] 
        Top,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BelowCandle))]
        Bottom,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ByCandleDirection))]
        CandleDirection
    }

    #endregion

    #region Fields

    private readonly PenSettings _bgPen = new() { Color = DefaultColors.Gray.Convert() };
    private readonly BrushSettings _bgBrush = new();
   
    private int _backGroundTransparency = 8;

    #endregion

    #region Properties

    #region Settings

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LabelLocation), GroupName = nameof(Strings.Settings))]
    public LabelLocations LabelLocation { get; set; } = LabelLocations.Top;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.Settings))]
    public bool ShowVolume { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDelta), GroupName = nameof(Strings.Settings))]
    public bool ShowDelta { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClustersMode), GroupName = nameof(Strings.Settings))]
    public bool ClusterModeOnly { get; set; }

    #endregion

    #region Visualization

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume), GroupName = nameof(Strings.Visualization))]
    public Color VolumeColor { get; set; } = DefaultColors.Blue;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveDelta), GroupName = nameof(Strings.Visualization))]
    public Color PositiveDeltaColor { get; set; } = DefaultColors.Green;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeDelta), GroupName = nameof(Strings.Visualization))]
    public Color NegativeDeltaColor { get; set; } = DefaultColors.Red;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Visualization))]
    public Color BackGroundColor 
    { 
        get => _bgPen.Color.Convert();
        set
        {
            _bgPen.Color = value.Convert();
            _bgBrush.StartColor = GetColorTransparency(value, BackGroundTransparency).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideBackGround), GroupName = nameof(Strings.Visualization))]
    public bool HideBackGround { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Visualization))]
    public FontSetting FontSetting { get; set; } = new("Trebuchet MS", 9);

    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Offset), GroupName = nameof(Strings.Visualization))]
    public int Offset { get; set; } = 10;

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderWidth), GroupName = nameof(Strings.Visualization))]
    public int BorderWidth 
    { 
        get => _bgPen.Width;
        set => _bgPen.Width = value;
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.Visualization))]
    public int BackGroundTransparency 
    { 
        get => _backGroundTransparency;
        set
        {
            _backGroundTransparency = value;
            _bgBrush.StartColor = GetColorTransparency(BackGroundColor, BackGroundTransparency).Convert();
        } 
    }

    #endregion

    #endregion

    #region ctor

    public CandleStatistics() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;

        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        _bgPen.Color = BackGroundColor.Convert();
        _bgBrush.StartColor = GetColorTransparency(BackGroundColor, _backGroundTransparency).Convert();
    }

    #endregion

    #region Protected methods

    protected override void OnCalculate(int bar, decimal value)
    {
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo == null)
            return;

        if (ClusterModeOnly && ChartInfo.ChartVisualMode != ChartVisualModes.Clusters)
            return;

        DrawLabels(context);
    }

    #endregion

    #region Private methods

    private void DrawLabels(RenderContext context)
    {
        if (!(ShowDelta || ShowVolume))
            return;

        for (int bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
        {
            var candle = GetCandle(bar);
            var delta = candle.Delta;
           
            var volumeStr = GetTrueString(candle.Volume);
            var deltaStr = GetTrueString(delta);

            var shiftBetweenStr = (int)(FontSetting.RenderObject.Size / 100 * 20);
            var shift = 2;

            var volumeSize = new Size();
            var deltaSize = new Size();

            if (ShowVolume)
                volumeSize = context.MeasureString(volumeStr, FontSetting.RenderObject);

            if (ShowDelta)
                deltaSize = context.MeasureString(deltaStr, FontSetting.RenderObject);

            var h = volumeSize.Height + deltaSize.Height + shift * 2 + shiftBetweenStr;
            var y = (int)GetStartY(candle, h);
            var x = ChartInfo.GetXByBar(bar);
            var volumeWidth = volumeSize.Width + 2;
            var deltaWidth = deltaSize.Width + 2;
            var w = Math.Max(volumeWidth, deltaWidth) + shift * 2;
            w = GetTrueWidth(w);
            
            var rectangle = new Rectangle(x, y, w, h);

            if (!HideBackGround)
                context.DrawFillRectangle(_bgPen.RenderObject, _bgBrush.RenderObject.StartColor, rectangle);

            if (ShowVolume)
            {
                y += shift;
                var w1 = GetTrueWidth(volumeWidth);
                var rec = new Rectangle(x + shift, y, w1, volumeSize.Height);
                context.DrawString(volumeStr, FontSetting.RenderObject, VolumeColor, rec);
            }

            if (ShowDelta)
            {
                y += volumeSize.Height > 0 ? volumeSize.Height + shiftBetweenStr : shift;
                var w1 = GetTrueWidth(deltaWidth);
                var rec = new Rectangle(x + shift, y, w1, deltaSize.Height);
                var color = delta < 0 ? NegativeDeltaColor : PositiveDeltaColor;
                context.DrawString(deltaStr, FontSetting.RenderObject, color, rec);
            }
        }
    }

    private string GetTrueString(decimal value)
    {
        var absValue = Math.Abs(value);

        if ((int)absValue < absValue)
            return value.ToString().TrimEnd('0');

        return value.ToString();
    }

    private int GetTrueWidth(int width)
    {
        return Math.Min(width, (int)ChartInfo.PriceChartContainer.BarsWidth);
    }

    private decimal GetStartY(IndicatorCandle candle, int height)
    {
        var topHeight = ChartInfo.GetYByPrice(candle.High) - Offset - height;
        var bottomHeight = ChartInfo.GetYByPrice(candle.Low) + Offset + ChartInfo.PriceChartContainer.PriceRowHeight;

        return LabelLocation switch
        {
            LabelLocations.Top => topHeight,
            LabelLocations.Bottom => bottomHeight,
            LabelLocations.CandleDirection => candle.Open > candle.Close
                                            ? bottomHeight
                                            : topHeight,
            _ => 0,
        };
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        var colorA = Math.Max(color.A - (tr * 25), 0);

        return Color.FromArgb((byte)colorA, color.R, color.G, color.B);
    }

    #endregion
}
