namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

[FeatureId("NotReady")]
[DisplayName("Order Block")]

public class OrderBlock : Indicator
{
    #region Nested types

    class Swing(int bar, decimal prise)
    {
        public int Bar { get; set; } = bar;
        public decimal Prise { get; set; } = prise;
        public bool IsCrossed { get; set; } = false;
        public bool IsBullish { get; set; }
    }

    class Block
    {
        public int Bar { get; set; }
        public decimal Top { get; set; }
        public decimal Btm { get; set; }
        public int BreakerBar { get; set; }
        public bool IsVisible { get; set; }
        public decimal PocPrice { get; set; }
    }

    #endregion

    #region Fields

    #region Readonly fields

    private readonly List<Block> _bullishBlocks = [];
    private readonly List<Block> _bearishBlocks = [];
    private readonly List<Block> _blocksForDelete = [];

    private readonly PenSettings _bullishPen = new() { Color = DefaultColors.Green.Convert(), Width = 1 };
    private readonly PenSettings _bearishPen = new() { Color = DefaultColors.Red.Convert(), Width = 1 };
    private readonly PenSettings _brokenBullishPen = new()
    {
        Color = DefaultColors.DarkRed.Convert(),
        Width = 1,
        LineDashStyle = LineDashStyle.DashDotDot
    };

    private readonly PenSettings _brokenBearishPen = new()
    {
        Color = DefaultColors.Lime.Convert(),
        Width = 1,
        LineDashStyle = LineDashStyle.DashDotDot
    };

    private readonly PenSettings _pocPen = new() { Color = DefaultColors.Blue.Convert(), Width = 1 };

    #endregion

    private int _lasBar = -1;
    private Swing? _topSwing;
    private Swing? _bottomSwing;
    private Swing? _lastSwing;

    private Block? _curBullishBlock;
    private Block? _curBearishBlock;
    private int? _os;

    private int _period = 10;
    private bool _usBody;

    #endregion

    #region Properties

    [Parameter]
    [Range(3, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings),
        Description = nameof(Strings.PeriodDescription))]
    public int Period 
    { 
        get => _period; 
        set
        {
            _period = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.IgnoreWicks), GroupName = nameof(Strings.Settings),
        Description = nameof(Strings.IgnoreWicksDescription))]
    public bool UsBody 
    {
        get => _usBody;
        set
        {
            _usBody = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.Settings),
       Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int Transparency { get; set; } = 5;

    #region Bullish

    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LastBullishElementsNumber), GroupName = nameof(Strings.Bullish))]
    public int BullishNumber { get; set; } = 3;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Bullish),
        Description = nameof(Strings.BullishColorDescription))]
    public Color BullishColor
    {
        get => _bullishPen.Color.Convert();
        set => _bullishPen.Color = value.Convert();
    } 

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishBreakColor), GroupName = nameof(Strings.Bullish),
        Description = nameof(Strings.BrokenBullishDescription))]
    public Color BullishBreakColor 
    {
        get=>_brokenBullishPen.Color.Convert();
        set=> _brokenBullishPen.Color = value.Convert();
    }

    #endregion

    #region Bearish

    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LastBearishElementsNumber), GroupName = nameof(Strings.Bearish))]
    public int BearishNumber { get; set; } = 3;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearishColor), GroupName = nameof(Strings.Bearish),
        Description = nameof(Strings.BearishColorDescription))]
    public Color BearishColor 
    {
        get => _bearishPen.Color.Convert();
        set => _bearishPen.Color = value.Convert();
    } 

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearishBreakColor), GroupName = nameof(Strings.Bearish),
       Description = nameof(Strings.BrokenBearishDescription))]
    public Color BearishBreakColor 
    {
        get => _brokenBearishPen.Color.Convert();
        set => _brokenBearishPen.Color = value.Convert();
    }

    #endregion

    #region POC Level

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowPocLevel), GroupName = nameof(Strings.PocLevel),
      Description = nameof(Strings.ShowPocLevelDescription))]
    public bool ShowPocLevel { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.PocLevel),
       Description = nameof(Strings.ColorDescription))]
    public Color PocColor 
    {
        get => _pocPen.Color.Convert();
        set => _pocPen.Color = value.Convert();
    }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.PocLevel),
        Description = nameof(Strings.LineWidthDescription))]
    public int PocWidth 
    {
        get => _pocPen.Width;
        set => _pocPen.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.PocLevel),
       Description = nameof(Strings.LineDashStyleDescription))]
    public LineDashStyle PocStyle
    {
        get => _pocPen.LineDashStyle;
        set => _pocPen.LineDashStyle = value;
    }

    #endregion

    #endregion

    #region ctor

    public OrderBlock() : base(true)
    {
        DenyToChangePanel = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        EnableCustomDrawing = true;

        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
    }

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        _bullishPen.Color = DefaultColors.Green.Convert();
        _bearishPen.Color = DefaultColors.Red.Convert();
        _brokenBullishPen.Color = DefaultColors.DarkRed.Convert();
        _brokenBearishPen.Color = DefaultColors.Lime.Convert();
        _pocPen.Color = DefaultColors.Blue.Convert();
    }

    protected override void OnRecalculate()
    {
        _bullishBlocks.Clear();
        _bearishBlocks.Clear();
        _blocksForDelete.Clear();

        _lastSwing = _topSwing = _bottomSwing = null;
        _os = 0;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (_lasBar != bar)
        {
            TrySetSwings(bar - 1);
            TrySetBullishBlock(bar - 1);
            TrySetBearishBlock(bar - 1);
            TryBreakBlocks(bar - 1);
        }

        _lasBar = bar;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null)
            return;

        DrawBlocks(context, _bullishBlocks, BullishNumber, _bullishPen, _brokenBullishPen, Transparency);
        DrawBlocks(context, _bearishBlocks, BearishNumber, _bearishPen, _brokenBearishPen, Transparency);
    }

    #endregion

    #region Private methods

    private void TryBreakBlocks(int bar)
    {
        if (bar < 0)
            return;

        var candle = GetCandle(bar);

        foreach (var block in _bullishBlocks)
        {
            if (block.BreakerBar == 0)
                block.BreakerBar = candle.Close < block.Btm ? bar : 0;
            else if (candle.Close > block.Top)
                _blocksForDelete.Add(block);
        }

        foreach (var block in _blocksForDelete)
            _bullishBlocks.Remove(block);

        _blocksForDelete.Clear();

        foreach (var block in _bearishBlocks)
        {
            if (block.BreakerBar == 0)
                block.BreakerBar = candle.Close > block.Top ? bar : 0;
            else if (candle.Close < block.Btm)
                _blocksForDelete.Add(block);
        }

        foreach (var block in _blocksForDelete)
            _bearishBlocks.Remove(block);

        _blocksForDelete.Clear();
    }

    private void TrySetBearishBlock(int bar)
    {
        if (bar < 0)
            return;

        var candle = GetCandle(bar);

        if (_curBearishBlock is { } && !_curBearishBlock.IsVisible && candle.High < _curBearishBlock.Btm)
            _curBearishBlock.IsVisible = true;

        if (_bottomSwing is null || _bottomSwing.IsCrossed || candle.Close >= _bottomSwing.Prise)
            return;

        _bottomSwing.IsCrossed = true;
        var swingBar = bar;
        var maxMin = GetCandleMaxMin(swingBar);
        var max = maxMin.Item1;
        var min = maxMin.Item2;

        for (int i = swingBar - 1; i > _bottomSwing.Bar; i--)
        {
            var maxMinLocal = GetCandleMaxMin(i);

            max = Math.Max(max, maxMinLocal.Item1);
            min = max == maxMinLocal.Item1 ? maxMinLocal.Item2 : min;
            swingBar = max == maxMinLocal.Item1 ? i : swingBar;
        }

        if (GetCandleForm(swingBar) != 1)
        {
            swingBar--;

            if (GetCandleForm(swingBar) != 1)
                return;

            maxMin = GetCandleMaxMin(swingBar);
            max = maxMin.Item1;
            min = maxMin.Item2;
        }

        if (_curBearishBlock is { } && !_curBearishBlock.IsVisible)
            _bearishBlocks.Remove(_curBearishBlock);

        _curBearishBlock = new Block
        {
            Bar = swingBar,
            Top = max,
            Btm = min,
            PocPrice = GetCandle(swingBar).MaxVolumePriceInfo.Price
        };

        _bearishBlocks.Add(_curBearishBlock);
    }

    private void TrySetBullishBlock(int bar)
    {
        if (bar < 0)
            return;

        var candle = GetCandle(bar);

        if (_curBullishBlock is { } && !_curBullishBlock.IsVisible && candle.Low > _curBullishBlock.Top)
            _curBullishBlock.IsVisible = true;

        if (_topSwing is null || _topSwing.IsCrossed || candle.Close <= _topSwing.Prise)
            return;

        _topSwing.IsCrossed = true;
        var swingBar = bar;
        var maxMin = GetCandleMaxMin(swingBar);
        var max = maxMin.Item1;
        var min = maxMin.Item2;       

        for (int i = swingBar - 1; i > _topSwing.Bar; i--) 
        {
            var maxMinLocal = GetCandleMaxMin(i);
                       
            min = Math.Min(min, maxMinLocal.Item2);
            max = min == maxMinLocal.Item2 ? maxMinLocal.Item1 : max;
            swingBar = min == maxMinLocal.Item2 ? i : swingBar; 
        }

        if (GetCandleForm(swingBar) != -1) 
        {
            swingBar--;

            if (GetCandleForm(swingBar) != -1)
                return;

            maxMin = GetCandleMaxMin(swingBar);
            max = maxMin.Item1;
            min = maxMin.Item2;
        }

        if (_curBullishBlock is { } && !_curBullishBlock.IsVisible)
            _bullishBlocks.Remove(_curBullishBlock);

        _curBullishBlock = new Block
        {
            Bar = swingBar,
            Top = max,
            Btm = min,
            PocPrice = GetCandle(swingBar).MaxVolumePriceInfo.Price
        };

        _bullishBlocks.Add(_curBullishBlock);
    }

    /// <summary>
    /// Determines whether the candle is rising or falling. 
    /// If the returned value is positive, it means the candle is growing, 
    /// if negative, it means the candle is falling.
    /// </summary>
    /// <param name="bar"></param>
    /// <returns></returns>
    private int GetCandleForm(int bar)
    {
        var candle = GetCandle(bar);

        return (candle.High - candle.Close) < (candle.Close - candle.Low)
            ? 1
            : (candle.High - candle.Close) > (candle.Close - candle.Low)
             ? -1
             : 0;
    }

    private (decimal, decimal) GetCandleMaxMin(int bar)
    {
        var candle = GetCandle(bar);

        return UsBody
            ? (Math.Max(candle.Open, candle.Close), Math.Min(candle.Open, candle.Close))
            : (candle.High, candle.Low);
    }

    private void TrySetSwings(int bar)
    {
        if (bar < _period)
            return;

        var b = bar - _period;
        var upper = GetLastHighest(bar);
        var lower = GetLastLowest(bar);
        var candle = GetCandle(b);
        int? os;

        if (_lastSwing is null || _lastSwing.IsBullish && b != _lastSwing.Bar) 
        {
            os = candle.High > upper ? 0 : _os;

            if (os == 0 && _os != 0)
            {
                _lastSwing = _topSwing = new(b, candle.High);
                _os = os;
            }
        }
        
        if(_lastSwing is null || !_lastSwing.IsBullish && b != _lastSwing.Bar)
        {
            os = candle.Low < lower ? 1 : _os;

            if (os == 1 && _os != 1)
            {
                _lastSwing = _bottomSwing = new(b, candle.Low) { IsBullish = true };
                _os = os;
            }
        }   
    }

    private decimal GetLastLowest(int bar)
    {
        var start = Math.Max(0, bar - _period + 1);
        var res = decimal.MaxValue;

        for (int i = start; i < bar; i++)
        {
            var candle = GetCandle(i);

            if (candle.Low < res)
                res = candle.Low;
        }

        return res;
    }

    private decimal GetLastHighest(int bar)
    {
        var start = Math.Max(0, bar - _period +1 );
        var res = 0m;

        for (int i = start; i < bar; i++)
        {
            var candle = GetCandle(i);

            if (candle.High > res)
                res = candle.High;
        }

        return res;
    }

    #region Drawing

    private void DrawBlocks(RenderContext context, List<Block> blocksList, int number, PenSettings pen, PenSettings breakPen, int transparency)
    {
        var blocks = blocksList.Where(b => b.IsVisible).TakeLast(number).ToList(); 

        foreach (var block in blocks)
        {
            if (block.Bar > LastVisibleBarNumber)
                continue;

            var isBroken = block.BreakerBar > 0;
            var x = ChartInfo.GetXByBar(block.Bar);
            var y = ChartInfo.GetYByPrice(block.Top);
            var w = (isBroken ? ChartInfo.GetXByBar(block.BreakerBar) : ChartArea.Right) - x;
            var h = ChartInfo.GetYByPrice(block.Btm) - y;
            var rec = new Rectangle(x, y, w, h);

            context.DrawFillRectangle(pen.RenderObject, GetColorTransparency(pen.RenderObject.Color, transparency), rec);

            if (ShowPocLevel)
            {
                var pocY = ChartInfo.GetYByPrice(block.PocPrice, false);
                context.DrawLine(_pocPen.RenderObject, x, pocY, ChartArea.Right, pocY);
            }

            if (!isBroken)
                continue;

            x = ChartInfo.GetXByBar(block.BreakerBar);
            w = ChartArea.Right - x;
            rec = new Rectangle(x, y, w, h);

            context.DrawFillRectangle(breakPen.RenderObject, GetColorTransparency(breakPen.RenderObject.Color, transparency), rec);
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        var colorA = Math.Max(color.A - (tr * 25), 0);

        return Color.FromArgb((byte)colorA, color.R, color.G, color.B);
    }

    #endregion

    #endregion
}
