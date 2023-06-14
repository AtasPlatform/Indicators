namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;
using OFT.Rendering.Context;

using Color = System.Drawing.Color;
using Pen = OFT.Rendering.Tools.RenderPen;

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

    private int _lastBar = -1;
    private SMA _sma;
    private Pen _bullishPen;
    private Pen _bearishPen;

    private bool _closeGapsPartially;
    private int _minDeviation = 30;
    private bool _limitMaxGapBodyLength;
    private int _maxGapBodyLength = 300;
    private Color _bullishColor = DefaultColors.Green;
    private Color _bearishColor = DefaultColors.Red;
    private float _borderWidth = 2f;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Resources), Name = "HideGaps", GroupName = "Visualization")]
    public bool HideGaps { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "BullishColor", GroupName = "Visualization")]
    public Color BullishColor 
    { 
        get => _bullishColor;
        set
        {
            _bullishColor = value;
            _bullishPen = new Pen(_bullishColor, _borderWidth);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "BearlishColor", GroupName = "Visualization")]
    public Color BearlishColor
    { 
        get => _bearishColor; 
        set
        {
            _bearishColor = value;
            _bearishPen = new Pen(_bearishColor, _borderWidth);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "HideBorder", GroupName = "Visualization")]
    public bool HideBorder { get; set; }

    [Range(1, 10)]
    [Display(ResourceType = typeof(Resources), Name = "BorderWidth", GroupName = "Visualization")]
    public float BorderWidth 
    {
        get => _borderWidth;
        set
        {
            _borderWidth = value;
            _bullishPen = new Pen(_bullishColor, _borderWidth);
            _bearishPen = new Pen(_bearishColor, _borderWidth);
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "Visualization")]
    public int Transparency { get; set; } = 6;

    [Display(ResourceType = typeof(Resources), Name = "ClosePartially", GroupName = "Settings")]
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
    [Display(ResourceType = typeof(Resources), Name = "MinimalDeviation", GroupName = "Settings")]
    public int MinDeviation 
    {
        get => _minDeviation; 
        set
        {
            _minDeviation = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "LimitMaxGapBodyLength", GroupName = "Settings")]
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
    [Display(ResourceType = typeof(Resources), Name = "MaxGapBodyLength", GroupName = "Settings")]
    public int MaxGapBodyLength 
    { 
        get => _maxGapBodyLength;
        set
        {
            _maxGapBodyLength = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts")]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts")]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts")]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts")]
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

        _bullishPen = new Pen(_bullishColor, _borderWidth);
        _bearishPen = new Pen(_bearishColor, _borderWidth);
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
                AddAlert(AlertFile, InstrumentInfo.Instrument, _closeGapMessage, AlertBGColor.Convert(), AlertForeColor.Convert());
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
            AddAlert(AlertFile, InstrumentInfo.Instrument, _closeGapMessage, AlertBGColor.Convert(), AlertForeColor.Convert());
    }

    private void TryRegisterNewGap(int bar, IndicatorCandle candle, IndicatorCandle prevCandle, decimal smaValue)
    {
        if (IsGapConditions(candle.Low, prevCandle.High, smaValue))
            CreateNewGap(bar - 1, candle.Low, bar, prevCandle.High, true);
        else if (IsGapConditions(prevCandle.Low, candle.High, smaValue))
            CreateNewGap(bar - 1, prevCandle.Low, bar, candle.High, false);
    }

    private bool IsGapConditions(decimal top, decimal bottom, decimal smaValue)
    {
        return (top - bottom) >= (smaValue / 100 * _minDeviation);
    }

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
            AddAlert(AlertFile, InstrumentInfo.Instrument, _newGapMessage, AlertBGColor.Convert(), AlertForeColor.Convert());
    }

    private void DrawGaps(RenderContext context)
    {
        foreach (var gap in _gaps)
        {
            var color = gap.IsBull ? BullishColor : BearlishColor;
            var pen = gap.IsBull ? _bullishPen : _bearishPen;

            foreach (var box in gap.Boxes)
            {
                if (box.Left > LastVisibleBarNumber || box.Right < FirstVisibleBarNumber)
                    continue;

                var isClusterMode = ChartInfo.ChartVisualMode == ChartVisualModes.Clusters;
                var x = ChartInfo.GetXByBar(box.Left, isClusterMode);
                var y = ChartInfo.GetYByPrice(box.Top, isClusterMode);
                var w = ChartInfo.GetXByBar(box.Right, isClusterMode) - x;
                var h = ChartInfo.GetYByPrice(box.Bottom, isClusterMode) - y;
                var rec = new Rectangle(x, y, w, h);

                if (HideBorder)
                    context.FillRectangle(GetColorTransparency(color, Transparency), rec);
                else
                    context.DrawFillRectangle(pen, GetColorTransparency(color, Transparency), rec);
            }  
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        return Color.FromArgb((byte)(tr * 25), color.R, color.G, color.B);
    }

    #endregion
}
