namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using Color = System.Windows.Media.Color;

[DisplayName("Gaps")]
public class Gaps : Indicator
{
    #region Nested Types

    internal class Box
    {
        internal int Left { get; set; }
        internal decimal Top { get; set; }
        internal int Right { get; set; }
        internal decimal Bottom { get; set; }
    }

    internal class Gap
    {
        internal bool IsActive { get; set; } = true;
        internal bool IsVisible { get; set; } = true;
        internal bool IsBull { get; set; }
        internal List<Box> Boxes { get; set; } = new();
    }

    #endregion

    #region Fields

    private const string _newGapMessage = "A new gap has appeared.";
    private const string _closeGapMessage = "A gap was closed.";
    private const int _smaPeriod = 14;
    private readonly List<Gap> _gaps = new();
    private readonly PenSettings _bullishPen = new() { Color = Drawing.DefaultColors.Green.Convert(), Width = 2 };
    private readonly PenSettings _bearishPen = new() { Color = Drawing.DefaultColors.Red.Convert(), Width = 2 };
    private readonly FontSetting _labelFont = new() { FontFamily = "Arial", Size = 10 };
    private readonly RenderStringFormat _format = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
    };

    private int _lastBar = -1;
    private SMA _sma;
    private System.Drawing.Color _bullishColorTransp;
    private System.Drawing.Color _bearishColorTransp;

    private bool _closeGapsPartially;
    private int _minDeviation = 30;
    private bool _limitMaxGapBodyLength;
    private int _maxGapBodyLength = 300;
    private int _transparency = 6;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideGaps), GroupName = nameof(Strings.Visualization))]
    public bool HideGaps { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization))]
    public Color BullishColor 
    { 
        get => _bullishPen.Color;
        set
        {
            _bullishPen.Color = value;
            _bullishColorTransp = GetColorTransparency(_bullishPen.Color, _transparency).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization))]
    public Color BearlishColor
    { 
        get => _bearishPen.Color; 
        set
        {
            _bearishPen.Color = value;
            _bearishColorTransp = GetColorTransparency(_bearishPen.Color, _transparency).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideBorder), GroupName = nameof(Strings.Visualization))]
    public bool HideBorder { get; set; }

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderWidth), GroupName = nameof(Strings.Visualization))]
    public int BorderWidth 
    {
        get => _bearishPen.Width;
        set
        {
            _bearishPen.Width = value;
            _bullishPen.Width = value;
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.Visualization))]
    public int Transparency 
    {
        get => _transparency; 
        set
        {
            _transparency = value;
            _bullishColorTransp = GetColorTransparency(_bullishPen.Color, value).Convert();
            _bearishColorTransp = GetColorTransparency(_bearishPen.Color, value).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClosePartially), GroupName = nameof(Strings.Settings))]
    public bool CloseGapsPartially 
    {
        get => _closeGapsPartially; 
        set
        {
            _closeGapsPartially = value;
            RecalculateValues();
        }
    }

    [Range(1, 100)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimalDeviation), GroupName = nameof(Strings.Settings))]
    public int MinDeviation 
    {
        get => _minDeviation; 
        set
        {
            _minDeviation = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LimitMaxGapBodyLength), GroupName = nameof(Strings.Settings))]
    public bool LimitMaxGapBodyLength 
    { 
        get => _limitMaxGapBodyLength;
        set
        {
            _limitMaxGapBodyLength = value;
            RecalculateValues();
        }
    }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaxGapBodyLength), GroupName = nameof(Strings.Settings))]
    public int MaxGapBodyLength 
    { 
        get => _maxGapBodyLength;
        set
        {
            _maxGapBodyLength = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Label))]
    public bool ShowLabel { get; set; } = true;

    [Range(1, 50)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Size), GroupName = nameof(Strings.Label))]
    public int LabelSize
    {
        get => _labelFont.Size;
        set => _labelFont.Size = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Label))]
    public Color LabelColor { get; set; } = Drawing.DefaultColors.Gray.Convert();

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetX), GroupName = nameof(Strings.Label))]
    public int LabelOffsetX { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY), GroupName = nameof(Strings.Label))]
    public int LabelOffsetY { get; set; } = 10;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts))]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts))]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts))]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts))]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    #endregion

    #region ctor

    public Gaps() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;

        SubscribeToDrawingEvents(DrawingLayouts.Final);
        EnableCustomDrawing = true;

        _bearishColorTransp = GetColorTransparency(_bearishPen.Color, _transparency).Convert();
        _bullishColorTransp = GetColorTransparency(_bullishPen.Color, _transparency).Convert();
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        _gaps.Clear();
        _sma = new() { Period = _smaPeriod };
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var candle = GetCandle(bar);
        var smaValue = _sma.Calculate(bar, candle.High - candle.Low);

        if (bar == 0) return;

        TryCloseAllGaps(bar, candle);

        if (bar != _lastBar)
        {
            var prevCandle = GetCandle(bar - 1);
            TryRegisterNewGap(bar, candle, prevCandle, smaValue);

            _lastBar = bar;
        }
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
       if(ChartInfo is null) return;

        if (!HideGaps)
            DrawGaps(context);
    }

    #endregion

    #region Private Methods

    private void TryCloseAllGaps(int bar, IndicatorCandle candle)
    {
        foreach (var gap in _gaps) 
        {
            if(!gap.IsActive) continue;

            var activeBox = gap.Boxes.Last();
            activeBox.Right = bar;

            if (_limitMaxGapBodyLength && (bar - activeBox.Left) >= _maxGapBodyLength)
            {
                if (!_closeGapsPartially)
                    FullClose(bar, candle, gap);
                else
                    gap.IsVisible = false;

                continue;
            }

            if ((gap.IsBull && candle.Low < activeBox.Top) || (!gap.IsBull && candle.High > activeBox.Bottom))
            {
                if (_closeGapsPartially)
                {
                    if ((gap.IsBull && candle.Low >= activeBox.Bottom) || (!gap.IsBull && candle.High <= activeBox.Top))
                        PartialClose(bar, candle, gap);
                    else 
                        FullClose(bar, candle, gap);
                }
                else
                    FullClose(bar, candle, gap);
            }
        }

        if (_gaps.RemoveAll(g => !g.IsVisible) > 0)
            if (UseAlerts && bar == CurrentBar - 1)
                AddAlert(AlertFile, InstrumentInfo.Instrument, _closeGapMessage, AlertBGColor, AlertForeColor);
    }

    private void PartialClose(int bar, IndicatorCandle candle, Gap gap)
    {
        var activeBox = gap.Boxes.Last();
        var box = new Box
        {
            Left = bar,
            Top = gap.IsBull ? candle.Low : activeBox.Top,
            Right = bar,
            Bottom = gap.IsBull ? activeBox.Bottom : candle.High
        };

        gap.Boxes.Add(box);
    }

    private void FullClose(int bar, IndicatorCandle candle, Gap gap)
    {
        gap.IsActive = false;

        if (UseAlerts && bar == CurrentBar - 1) 
            AddAlert(AlertFile, InstrumentInfo.Instrument, _closeGapMessage, AlertBGColor, AlertForeColor);
    }

    private void TryRegisterNewGap(int bar, IndicatorCandle candle, IndicatorCandle prevCandle, decimal smaValue)
    {
        if (IsGapConditions(candle.Low, prevCandle.High, smaValue))
            CreateNewGap(bar - 1, candle.Low, bar, prevCandle.High, true);
        else if (IsGapConditions(prevCandle.Low, candle.High, smaValue))
            CreateNewGap(bar - 1, prevCandle.Low, bar, candle.High, false);
    }

    private bool IsGapConditions(decimal top, decimal bottom, decimal smaValue) => (top - bottom) >= (smaValue / 100 * _minDeviation);

    private void CreateNewGap(int left, decimal top, int right, decimal bottom, bool isBull)
    {
        var box = new Box
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom,
        };

        var gap = new Gap { IsBull = isBull };
        gap.Boxes.Add(box);

        _gaps.Add(gap);

        if ((UseAlerts && right == CurrentBar - 1))
            AddAlert(AlertFile, InstrumentInfo.Instrument, _newGapMessage, AlertBGColor, AlertForeColor);
    }

    private void DrawGaps(RenderContext context)
    {
        foreach (var gap in _gaps)
        {
            var color = gap.IsBull ? _bullishColorTransp : _bearishColorTransp;
            var pen = gap.IsBull ? _bullishPen.RenderObject : _bearishPen.RenderObject;
            var isClusterMode = ChartInfo.ChartVisualMode == ChartVisualModes.Clusters;
            var xGap = ChartInfo.GetXByBar(gap.Boxes[0].Left, isClusterMode);
            var yGap = ChartInfo.GetYByPrice(gap.Boxes[0].Top, isClusterMode);

            foreach (var box in gap.Boxes)
            {
                if (box.Left > LastVisibleBarNumber || box.Right < FirstVisibleBarNumber)
                    continue;

                var x = ChartInfo.GetXByBar(box.Left, isClusterMode);
                var y = ChartInfo.GetYByPrice(box.Top, isClusterMode);
                var w = ChartInfo.GetXByBar(box.Right, isClusterMode) - x;
                var h = ChartInfo.GetYByPrice(box.Bottom, isClusterMode) - y;
                var rec = new Rectangle(x, y, w, h);

                if (HideBorder)
                    context.FillRectangle(color, rec);
                else
                    context.DrawFillRectangle(pen, color, rec);
            }

            if (ShowLabel)
            {
                var timeFrame = ChartInfo.TimeFrame;
                var text = $"{timeFrame} Gap";
                var labelSize = context.MeasureString(text, _labelFont.RenderObject);
                var lX = xGap + ChartInfo.PriceChartContainer.BarsWidth * LabelOffsetX;
                var lY = yGap + ChartInfo.PriceChartContainer.PriceRowHeight * LabelOffsetY;
                var lRec = new Rectangle((int)lX, (int)lY, labelSize.Width, labelSize.Height);
                context.DrawString(text, _labelFont.RenderObject, LabelColor.Convert(), lRec, _format);
            }
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        var colorA = Math.Max(color.A - (tr * 25), 0);

        return Color.FromArgb((byte)colorA, color.R, color.G, color.B);
    }

    #endregion
}
