namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;

[DisplayName("Linear Regression Channel")]
public class LinRegChannel : Indicator
{
    #region Nested Types

    public enum InputType
    {
        [Display(ResourceType = typeof(Resources), Name = "Open")]
        Open,

        [Display(ResourceType = typeof(Resources), Name = "High")]
        High,

        [Display(ResourceType = typeof(Resources), Name = "Low")]
        Low,

        [Display(ResourceType = typeof(Resources), Name = "Close")]
        Close,

        [Display(ResourceType = typeof(Resources), Name = "HighLow2")]
        HighLow2,

        [Display(ResourceType = typeof(Resources), Name = "HighLowClose3")]
        HighLowClose3,

        [Display(ResourceType = typeof(Resources), Name = "OpenHighLowClose4")]
        OpenHighLowClose4,

        [Display(ResourceType = typeof(Resources), Name = "HighLow2Close4")]
        HighLow2Close4
    }

    internal enum DirectionTypes
    {
        Up,
        UpRight,
        Down,
        DownRight,
        Right
    }

    #endregion

    #region Fields

    private readonly decimal[] _fiboRatios = new decimal[] { 0.236m, 0.382m, 0.618m, 0.764m };
    private readonly ValueDataSeries _data = new("data");
    private readonly ValueDataSeries _slope = new("slope");
    private readonly ValueDataSeries _currDev = new("currDev");
    private readonly ValueDataSeries _y1 = new("y1");
    private readonly ValueDataSeries _y2 = new("y2");
    private readonly ValueDataSeries _outOfChannel = new("outOfChannel");

    private Pen _bullishPen = new(DefaultColors.Green) { Width = 2 };
    private Pen _bearishPen = new(DefaultColors.Red) { Width = 2 };
    private Pen _bullishDashPen = new(DefaultColors.Green) { Width = 2, DashStyle = DashStyle.Dash };
    private Pen _bearishDashPen = new(DefaultColors.Red) { Width = 2, DashStyle = DashStyle.Dash };
    private Pen _bullishFiboPen = new(DefaultColors.Green) { Width = 2, DashStyle = DashStyle.Dot };
    private Pen _bearishFiboPen = new(DefaultColors.Red) { Width = 2, DashStyle = DashStyle.Dot };
    private Pen _brokenPen = new(DefaultColors.Blue) { Width = 2, DashStyle = DashStyle.Dot };
    private PenSettings _arrowPen = new() { Color = DefaultColors.Black.Convert() };

    private Color _bullishColorTransparent;
    private Color _bearishColorTransparent;

    private TrendLine _main;
    private TrendLine _lower;
    private TrendLine _upper;
    private TrendLine _fibo1;
    private TrendLine _fibo2;
    private TrendLine _fibo3;
    private TrendLine _fibo4;
    private TrendLine _broken;

    private LinRegSlope _linRegSlope;
    private int _lastBar = -1;
    private int _realPeriod;

    private InputType _type = InputType.Close;
    private int _period = 100;
    private decimal _deviation = 2m;
    private bool _showFibonacci = true;
    private bool _showBrokenChannel = true;
    private bool _extendLines;
    private int _arrowSize = 2;
    private int _labelTransparency = 8;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation")]
    public InputType Type 
    { 
        get => _type;
        set
        {
            _type = value;
            RecalculateValues();
        }
    }

    [Range(10, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Calculation")]
    public int Period 
    {
        get => _period;
        set
        {
            _period = value;
            RecalculateValues();
        }
    }

    [Range(0.1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "Deviation", GroupName = "Calculation")]
    public decimal Deviation 
    {
        get => _deviation;
        set
        {
            _deviation = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ExtendLines", GroupName = "Visualization")]
    public bool ExtendLines 
    { 
        get => _extendLines; 
        set
        {
            _extendLines = value;

            SetExtendLine(_main, _extendLines);
            SetExtendLine(_lower, _extendLines);
            SetExtendLine(_upper, _extendLines);
            SetExtendLine(_fibo1, _extendLines);
            SetExtendLine(_fibo2, _extendLines);
            SetExtendLine(_fibo3, _extendLines);
            SetExtendLine(_fibo4, _extendLines);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ShowFibonacci", GroupName = "Visualization")]
    public bool ShowFibonacci
    { 
        get => _showFibonacci;
        set
        {
            _showFibonacci = value;
            RecalculateValues();
        } 
    }

    [Display(ResourceType = typeof(Resources), Name = "ShowBrokenChannel", GroupName = "Visualization")]
    public bool ShowBrokenChannel 
    { 
        get => _showBrokenChannel;
        set
        {
            _showBrokenChannel = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "BullishColor", GroupName = "Visualization")]
    public Color BullishColor 
    {
        get => _bullishPen.Color;
        set
        {
            _bullishPen.Color = value;
            _bullishDashPen.Color = value;
            _bullishFiboPen.Color = value;
            _bullishColorTransparent = GetColorTransparency(_bullishPen.Color, _labelTransparency);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "BearlishColor", GroupName = "Visualization")]
    public Color BearishColor 
    {
        get => _bearishPen.Color; 
        set
        {
            _bearishPen.Color = value;
            _bearishDashPen.Color = value;
            _bearishFiboPen.Color = value;
            _bearishColorTransparent = GetColorTransparency(_bearishPen.Color, _labelTransparency);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "BrokenChannelColor", GroupName = "Visualization")]
    public Color BrokenChannelColor 
    {
        get => _brokenPen.Color;
        set => _brokenPen.Color = value;
    }

    [Range(1, 20)]
    [Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Visualization")]
    public float LineWidth 
    {
        get => _bullishPen.Width;
        set
        {
            _bullishPen.Width = value;
            _bullishFiboPen.Width = value;
            _bullishDashPen.Width = value;
            _bearishPen.Width = value;
            _bearishFiboPen.Width = value;
            _bearishDashPen.Width = value;
            _brokenPen.Width = value;
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Visualization")]
    public Color ArrowColor 
    {
        get => _arrowPen.Color.Convert();
        set => _arrowPen.Color = value.Convert();
    }

    [Range(1, 20)]
    [Display(ResourceType = typeof(Resources), Name = "TextSize", GroupName = "Visualization")]
    public int ArrowSize
    {
        get => _arrowSize;
        set
        {
            _arrowPen.Width = Math.Min(value, 2);
            _arrowSize = value;
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "Visualization")]
    public int LabelTransparency 
    { 
        get => _labelTransparency;
        set
        {
            _labelTransparency = value;
            _bullishColorTransparent = GetColorTransparency(_bullishPen.Color, _labelTransparency);
            _bearishColorTransparent = GetColorTransparency(_bearishPen.Color, _labelTransparency);
        }
    }

    #endregion

    #region ctor

    public LinRegChannel() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        DrawAbovePrice = true;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        _bullishColorTransparent = GetColorTransparency(_bullishPen.Color, _labelTransparency);
        _bearishColorTransparent = GetColorTransparency(_bearishPen.Color, _labelTransparency);
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        _realPeriod = _period > CurrentBar ? CurrentBar : _period;
        _linRegSlope = new LinRegSlope() { Period = _realPeriod };
        TrendLines.Clear();
        _main = _upper = _lower = _fibo1 = _fibo2 = _fibo3 = _fibo4 = null;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        SetChannel(bar);
        var dev = RoundToFraction(_currDev[bar] * _deviation, InstrumentInfo.TickSize);

        if (ShowBrokenChannel)
            BreakdownTest(bar, dev);

        if ( bar == CurrentBar - 1) 
        {
            SetLinRegLine(bar, ref _lower, -dev, _bullishDashPen, _bearishDashPen);
            SetLinRegLine(bar, ref _main, 0, _bullishPen, _bearishPen);
            SetLinRegLine(bar, ref _upper, dev, _bullishDashPen, _bearishDashPen);

            if (ShowFibonacci)
            {
                SetFiboLine(bar, ref _fibo1, _fiboRatios[0], _bullishFiboPen, _bearishFiboPen);
                SetFiboLine(bar, ref _fibo2, _fiboRatios[1], _bullishFiboPen, _bearishFiboPen);
                SetFiboLine(bar, ref _fibo3, _fiboRatios[2], _bullishFiboPen, _bearishFiboPen);
                SetFiboLine(bar, ref _fibo4, _fiboRatios[3], _bullishFiboPen, _bearishFiboPen);
            }
        }

        _lastBar = bar;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null) return;

        DrawLabel(context);
    }

    #endregion

    #region Private Methods

    #region Drawing

    private void DrawLabel(RenderContext context)
    {
        if (CurrentBar < 2) return;

        var slope = _slope[CurrentBar - 1];
        var slopePrev = _slope[CurrentBar - 2];
        var direction = GetSlopeDirection(slope, slopePrev);
        var startPoint = GetStartPoint(direction);

        var shift = 10;
        var arrowSize = new Size(shift * _arrowSize, shift * _arrowSize);
        var w = arrowSize.Width * 3;
        var h = arrowSize.Height * 3;
        var labelExtendPoints = GetLabelExtendPoints(startPoint, shift, direction);
        var labelStart = GetLabelStart(startPoint, w, h, direction, shift);
        var rec = new Rectangle(labelStart.X, labelStart.Y, w, h);
        var arrowPoints = GetArrowPoints(direction, arrowSize, rec, shift);
        var color = direction == DirectionTypes.Up || direction == DirectionTypes.UpRight
                  ? _bullishColorTransparent
                  : _bearishColorTransparent;

        context.FillPolygon(color, new Point[] { startPoint, labelExtendPoints[0], labelExtendPoints[1] });
        context.FillRectangle(color, rec);
        context.DrawLines(_arrowPen.RenderObject, arrowPoints);
    }

    private Point GetLabelStart(Point startPoint, int w, int h, DirectionTypes direction, int shift)
    {
        int x;
        int y;
        switch (direction)
        {
            case DirectionTypes.Up:
                x = startPoint.X - w / 2;
                y = startPoint.Y + shift;
                break;
            case DirectionTypes.UpRight:
                x = startPoint.X - shift - w;
                y = startPoint.Y + shift;
                break;
            case DirectionTypes.Down:
                x = startPoint.X - w / 2;
                y = startPoint.Y - shift - h;
                break;
            case DirectionTypes.DownRight:
                x = startPoint.X - shift - w;
                y = startPoint.Y - shift - h;
                break;
            default:
                x = startPoint.X - shift - w;
                y = startPoint.Y - h / 2;
                break;
        }

        return new Point(x, y);
    }

    private Point[] GetLabelExtendPoints(Point startPoint, int shift, DirectionTypes direction)
    {
        int x1;
        int y1;
        int x2;
        int y2;
        switch (direction)
        {
            case DirectionTypes.Up:
                x1 = startPoint.X - shift;
                y1 = y2 = startPoint.Y + shift;
                x2 = startPoint.X + shift;
                break;
            case DirectionTypes.UpRight:
                x1 = x2 = startPoint.X - shift;
                y1 = startPoint.Y + shift;
                y2 = y1 + shift;
                break;
            case DirectionTypes.Down:
                x1 = startPoint.X - shift;
                y1 = y2 = startPoint.Y - shift;
                x2 = startPoint.X + shift;
                break;
            case DirectionTypes.DownRight:
                x1 = x2 = startPoint.X - shift;
                y1 = startPoint.Y - shift;
                y2 = y1 - shift;
                break;
            default:
                x1 = x2 = startPoint.X - shift;
                y1 = startPoint.Y - shift;
                y2 = startPoint.Y + shift;
                break;
        }

        return new Point[] { new Point(x1, y1), new Point(x2, y2) };
    }

    private Point[] GetArrowPoints(DirectionTypes direction, Size arrowSize, Rectangle rec, int shift)
    {
        return direction switch
        {
            DirectionTypes.UpRight => GetArrowPointsRotated(arrowSize, rec, 45, -shift / 2 * _arrowSize, shift / 2),
            DirectionTypes.Right => GetArrowPointsRotated(arrowSize, rec, 90, -shift * _arrowSize, 0),
            DirectionTypes.DownRight => GetArrowPointsRotated(arrowSize, rec, 135, -shift * _arrowSize, -shift / 2 * _arrowSize),
            DirectionTypes.Down => GetArrowPointsRotated(arrowSize, rec, 180, -shift * _arrowSize, -shift * _arrowSize),
            _ => GetArrowPointsBase(arrowSize, rec),
        };
    }

    private Point[] GetArrowPointsRotated(Size arrowSize, Rectangle rec, double angle, int shiftX, int shiftY)
    {
        var points = GetArrowPointsBase(arrowSize, rec);
        var x0 = points.Select(p => p.X).Max();
        var y0 = points.Select(p => p.Y).Max();
        var poiint0 = new Point(x0, y0);
        var newPoints = new Point[points.Length];
        double angleRadian = angle * Math.PI / 180;

        for (int i = 0; i < newPoints.Length; i++)
        {
            var x = (points[i].X - x0) * Math.Cos(angleRadian) - (points[i].Y - y0) * Math.Sin(angleRadian) + x0 + shiftX;
            var y = (points[i].X - x0) * Math.Sin(angleRadian) + (points[i].Y - y0) * Math.Cos(angleRadian) + y0 + shiftY;
            newPoints[i] = new Point((int)x, (int)y);
        }

        return newPoints;
    }

    private Point[] GetArrowPointsBase(Size arrowSize, Rectangle labelRec)
    {
        var shift = arrowSize.Width / 2;
        var shift1 = shift / 5 * 3;
        var point1 = new Point(labelRec.X + arrowSize.Width + shift1, labelRec.Y + arrowSize.Height * 2);
        var point2 = new Point(point1.X, point1.Y - arrowSize.Height + shift);
        var point3 = new Point(point2.X - shift1, point2.Y);
        var point4 = new Point(point3.X + shift, point2.Y - shift - shift1);
        var point5 = new Point(point4.X + shift, point2.Y);
        var point6 = new Point(point5.X - shift1, point5.Y);
        var point7 = new Point(point6.X, point1.Y);

        return new Point[] {point1, point2, point3, point4, point5, point6, point7};    
    }

    private Point GetStartPoint(DirectionTypes direction)
    {
        var bar = CurrentBar - 1;
        var dev = RoundToFraction(_currDev[bar] * _deviation, InstrumentInfo.TickSize);
        decimal price;
        switch (direction)
        {
            case DirectionTypes.Up:
            case DirectionTypes.UpRight:
                price = _y1[bar] - dev;
                break;
            case DirectionTypes.Down:
            case DirectionTypes.DownRight:
                price = _y1[bar] + dev;
                break;
            default:
                price = _y1[bar];
                break;
        }

        return new Point(ChartInfo.GetXByBar(bar - _realPeriod + 1, false), ChartInfo.GetYByPrice(price, false));
    }

    private DirectionTypes GetSlopeDirection(decimal slope, decimal slopePrev)
    {
        switch (slope)
        {
            case > 0:
                if (slope > slopePrev)
                    return DirectionTypes.Up;
                else
                    return DirectionTypes.UpRight;
            case < 0:
                if (slope < slopePrev)
                    return DirectionTypes.Down;
                else
                    return DirectionTypes.DownRight;
            default:
                return DirectionTypes.Right;
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5) => Color.FromArgb((byte)(tr* 25), color.R, color.G, color.B);

    #endregion

    private void BreakdownTest(int bar, decimal dev)
    {
        if (bar < 0) return;

        if (bar != _lastBar)
        {
            _broken = null;
        }

        var candle = GetCandle(bar);

        if (_slope[bar] > 0 && candle.Close < (_y2[bar] - _currDev[bar] * _deviation)) 
        {
            if (_outOfChannel[bar - 1] == 0)
                SetLinRegLine(bar - 1, ref _broken, -dev, _brokenPen, _brokenPen, true);

            _outOfChannel[bar] = 1;

            return;
        }
        else if(_slope[bar] < 0 && candle.Close > (_y2[bar] + _currDev[bar] * _deviation))
        {
            if (_outOfChannel[bar - 1] == 0)
                SetLinRegLine(bar - 1, ref _broken, dev, _brokenPen, _brokenPen, true);

            _outOfChannel[bar] = 1;

            return;
        }

        if (_broken is not null)
        {
            TrendLines.Remove(_broken);
            _broken = null;
        }

        _outOfChannel[bar] = 0;
    }

    private void SetExtendLine(TrendLine line, bool extendLines)
    {
        if (line is null) return;

        line.IsRay = extendLines;
    }

    private decimal GetSource(IndicatorCandle candle)
    {
        return _type switch
        {
            InputType.Close => candle.Close,
            InputType.Open => candle.Open,
            InputType.High => candle.High,
            InputType.Low => candle.Low,
            InputType.HighLow2 => (candle.High + candle.Low) / 2,
            InputType.HighLowClose3 => (candle.High + candle.Low + candle.Close) / 3,
            InputType.OpenHighLowClose4 => (candle.Open + candle.High + candle.Low + candle.Close) / 4,
            InputType.HighLow2Close4 => (candle.High + candle.Low + 2 * candle.Close) / 4,
            _ => 0
        };
    }

    private void SetChannel(int bar)
    {
        var candle = GetCandle(bar);
        var sourceVal = GetSource(candle);
        _data[bar] = sourceVal;
        _slope[bar] = _linRegSlope.Calculate(bar, sourceVal);
        var mid = _data.CalcAverage(_realPeriod, bar);
        _y1[bar] = mid - _slope[bar] * (_realPeriod / 2) + (1 - _realPeriod % 2) / 2 * _slope[bar];
        _y2[bar] = _y1[bar] + _slope[bar] * (_realPeriod - 1);

        _y1[bar] = RoundToFraction(_y1[bar], InstrumentInfo.TickSize);
        _y2[bar] = RoundToFraction(_y2[bar], InstrumentInfo.TickSize);

        var dev = 0m;

        for (int i = bar - _realPeriod + 1; i <= bar; i++) 
        {
            var res = _data[i] - (_slope[bar] * (_realPeriod - (bar - i)) + _y1[bar]);
            dev += res * res;
        }

        _currDev[bar] = (decimal)Math.Sqrt((double)(dev / _realPeriod));
    }

    private void SetLinRegLine(int bar, ref TrendLine line, decimal dev, Pen bullishPen, Pen bearishPen, bool isBrokenLine = false)
    {
        var x1 = bar - _realPeriod + 1;
        var y1 = _y1[bar] + dev;
        var x2 = bar;
        var y2 = _y2[bar] + dev;
        var pen = _slope[bar] > 0 ? bullishPen : bearishPen;
        SetTrendLine(ref line, x1, y1, x2, y2, pen, isBrokenLine);
    }

    private decimal RoundToFraction(decimal value, decimal fraction) => Math.Round(value / fraction) * fraction;

    private void SetFiboLine(int bar, ref TrendLine line, decimal fiboRatio, Pen bullishFiboPen, Pen bearishFiboPen)
    {
        var dev = _currDev[bar] * _deviation - _currDev[bar] * _deviation * 2 * fiboRatio;
        dev = RoundToFraction(dev, InstrumentInfo.TickSize);
        var x1 = bar - _realPeriod + 1;
        var y1 = _y1[bar] - dev;
        var x2 = bar;
        var y2 = _y2[bar] - dev;
        var pen = _slope[bar] > 0 ? bullishFiboPen : bearishFiboPen;
        SetTrendLine(ref line, x1, y1, x2, y2, pen);
    }

    private void SetTrendLine(ref TrendLine line, int x1, decimal y1, int x2, decimal y2, Pen pen, bool isBrokenLine = false)
    {
        if (line is null)
        {
            line = new TrendLine(x1, y1, x2, y2, pen) { IsRay = !isBrokenLine && _extendLines };
            TrendLines.Add(line);
        }
        else
        {
            line.FirstBar = x1;
            line.FirstPrice = y1;
            line.SecondBar = x2;
            line.SecondPrice = y2;
            line.Pen = pen;
        }
    }

    #endregion
}
