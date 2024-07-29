namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = CrossColor;

[DisplayName("Cluster Statistic")]
[Category("Clusters, Profiles, Levels")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ClusterStatisticDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602624")]
public class ClusterStatistic : Indicator
{
    #region Static and constants

    private const int _headerOffset = 3;
    private const int _headerWidth = 140;

    #endregion

    #region Fields

    private readonly ValueDataSeries _candleDurations = new("durations");
    private readonly ValueDataSeries _candleHeights = new("heights");
    private readonly ValueDataSeries _cDelta = new("cDelta");
    private readonly ValueDataSeries _cVolume = new("cVolume");
    private readonly ValueDataSeries _deltaPerVol = new("DeltaPerVol");
    private readonly ValueDataSeries _volPerSecond = new("VolPerSecond");

    private readonly RenderStringFormat _stringLeftFormat = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private int _lastBar = -1;
    private Color _backGroundColor = Color.FromArgb(120, 0, 0, 0);
    private bool _centerAlign;
    private decimal _cumVolume;

    private int _height = 15;
    private int _lastDeltaAlert;
    private decimal _lastDeltaValue;
    private int _lastVolumeAlert;
    private decimal _lastVolumeValue;
    private decimal _maxAsk;
    private decimal _maxBid;
    private decimal _maxDelta;
    private decimal _maxDeltaChange;
    private decimal _maxDeltaPerVolume;
    private decimal _maxDuration;
    private decimal _maxHeight;
    private decimal _maxMaxDelta;
    private decimal _maxMinDelta;
    private decimal _maxSessionDelta;
    private decimal _maxSessionDeltaPerVolume;
    private decimal _maxTicks;
    private decimal _maxVolume;
    private decimal _minDelta;

    #endregion

    #region Properties

    #region Rows

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAsk), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowAsksDescription), Order = 110)]
    public bool ShowAsk { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBid), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowBidsDescription), Order = 110)]
    public bool ShowBid { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDelta), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowDeltaDescription), Order = 120)]
    public bool ShowDelta { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDeltaPerVolume), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowDeltaPerVolumeDescription), Order = 130)]
    public bool ShowDeltaPerVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionDelta), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowSessionDeltaDescription), Order = 140)]
    public bool ShowSessionDelta { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionDeltaPerVolume), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowSessionDeltaPerVolumeDescription), Order = 150)]
    public bool ShowSessionDeltaPerVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowMaximumDelta), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowMaximumDeltaDescription), Order = 160)]
    public bool ShowMaximumDelta { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowMinimumDelta), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowMinimumDeltaDescription), Order = 170)]
    public bool ShowMinimumDelta { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDeltaChange), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowDeltaChangeDescription), Order = 175)]
    public bool ShowDeltaChange { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowVolumesDescription), Order = 180)]
    public bool ShowVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolumePerSecond), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowVolumePerSecondDescription), Order = 190)]
    public bool ShowVolumePerSecond { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSessionVolume), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowSessionVolumeDescription), Order = 191)]
    public bool ShowSessionVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTradesCount), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowTradesCountDescription), Order = 192)]
    public bool ShowTicks { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowHeight), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowCandleHeightDescription), Order = 193)]
    public bool ShowHighLow { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTime), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowCandleTimeDescription), Order = 194)]
    public bool ShowTime { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDuration), GroupName = nameof(Strings.Rows), Description = nameof(Strings.ShowCandleDurationDescription), Order = 196)]
    public bool ShowDuration { get; set; }

    #endregion

    #region Colors

    [Display(ResourceType = typeof(Strings), Name = "BackGround", GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelFillColorDescription), Order = 200)]
    public Color BackGroundColor
    {
	    get => _backGroundColor;
	    set => _backGroundColor = Color.FromArgb(120, value.R, value.G, value.B);
    }


    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Grid), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.GridColorDescription), Order = 210)]
    public Color GridColor { get; set; } = CrossColors.Transparent;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisibleProportion), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisibleProportionDescription), Order = 220)]
    public bool VisibleProportion { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VolumeColorDescription), Order = 230)]
    public Color VolumeColor { get; set; } = CrossColors.DarkGray;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.AskColorDescription), Order = 240)]
    public Color AskColor { get; set; } = CrossColors.Green;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BidColorDescription), Order = 250)]
    public Color BidColor { get; set; } = CrossColors.Red;

    #endregion

    #region Text

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Text), Description = nameof(Strings.LabelTextColorDescription), Order = 300)]
    public Color TextColor { get; set; } = CrossColors.White;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Text), Description = nameof(Strings.FontSettingDescription), Order = 310)]
    public FontSetting Font { get; set; } = new("Arial", 9);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CenterAlign), GroupName = nameof(Strings.Text), Description = nameof(Strings.CenterAlignDescription), Order = 320)]
    public bool CenterAlign
    {
        get => _centerAlign;
        set
        {
            _centerAlign = value;
            _stringLeftFormat.Alignment = value ? StringAlignment.Center : StringAlignment.Near;
        }
    }

    #endregion

    #region Headers

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Headers), Description = nameof(Strings.HeaderBackgroundDescription), Order = 330)]
    public Color HeaderBackground { get; set; } = Color.FromRgb(84, 84, 84);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideRowsDescription), GroupName = nameof(Strings.Headers), Description = nameof(Strings.HideHeadersDescription), Order = 340)]
    public bool HideRowsDescription { get; set; }

    #endregion

    #region Volume Alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.UseAlertDescription), Order = 400)]
    public bool UseVolumeAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.AlertFilterDescription), Order = 410)]
    [Range(0, int.MaxValue)]
    public decimal VolumeAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.VolumeAlert), Description = nameof(Strings.AlertFileDescription), Order = 420)]
    public string VolumeAlertFile { get; set; } = "alert1";

    #endregion

    #region Delta alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.UseAlertDescription), Order = 500)]
    public bool UseDeltaAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.AlertFilterDescription), Order = 510)]
    public decimal DeltaAlertValue { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.DeltaAlert), Description = nameof(Strings.AlertFileDescription), Order = 520)]
    public string DeltaAlertFile { get; set; } = "alert1";

    #endregion

    #endregion

    #region ctor

    public ClusterStatistic()
        : base(true)
    {
        DenyToChangePanel = true;
        Panel = IndicatorDataProvider.NewPanel;
        EnableCustomDrawing = true;
        ShowDelta = ShowSessionDelta = ShowVolume = true;
        SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);

        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        ShowDescription = false;
    }

    #endregion

    #region Protected methods

    protected override void OnInitialize()
    {
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        base.OnInitialize();
    }

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        BidColor = ChartInfo.ColorsStore.FootprintBidColor.Convert();
        AskColor = ChartInfo.ColorsStore.FootprintAskColor.Convert();
        VolumeColor = ChartInfo.ColorsStore.PaneSeparators.Color.Convert();
        GridColor = ChartInfo.ColorsStore.Grid.Color.Convert();
        HeaderBackground = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        BackGroundColor = ChartInfo.ColorsStore.BaseBackgroundColor.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var candle = GetCandle(bar);

        var candleSeconds = Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds);

        if (candleSeconds is 0)
            candleSeconds = 1;

        _volPerSecond[bar] = candle.Volume / candleSeconds;

        if (bar == 0)
        {
            _cumVolume = 0;
            _maxVolume = 0;
            _maxDelta = 0;
            _maxMaxDelta = 0;
            _maxMinDelta = 0;
            _maxDeltaChange = 0;
            _minDelta = decimal.MaxValue;
            _maxHeight = 0;
            _maxTicks = 0;
            _maxDuration = 0;
            _maxSessionDelta = 0;
            _maxDeltaPerVolume = 0;
            _maxSessionDeltaPerVolume = 0;
            _maxBid = _maxAsk = 0;
            _cDelta[bar] = candle.Delta;
            return;
        }

        var prevCandle = GetCandle(bar - 1);

        if (IsNewSession(bar))
        {
            _cVolume[bar] = _cumVolume = candle.Volume;
            _cDelta[bar] = candle.Delta;
        }
        else
        {
            _cumVolume = _cVolume[bar] = _cVolume[bar - 1] + candle.Volume;
            _cDelta[bar] = _cDelta[bar - 1] + candle.Delta;
        }

        _maxSessionDelta = Math.Max(Math.Abs(_cDelta[bar]), _maxSessionDelta);

        _maxAsk = Math.Max(candle.Ask, _maxAsk);
        _maxBid = Math.Max(candle.Ask, _maxBid);

        _maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), _maxDeltaChange);

        _maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

        _maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), _maxMaxDelta);
        _maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), _maxMinDelta);

        _maxVolume = Math.Max(candle.Volume, _maxVolume);

        _minDelta = Math.Min(candle.MinDelta, _minDelta);


        _maxDeltaPerVolume = candle.Volume != 0
            ? Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), _minDelta)
            : 0;

        var candleHeight = candle.High - candle.Low;
        _maxHeight = Math.Max(candleHeight, _maxHeight);
        _candleHeights[bar] = candleHeight;

        _maxTicks = Math.Max(candle.Ticks, _maxTicks);

        _candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;
        _maxDuration = Math.Max(_candleDurations[bar], _maxDuration);

        if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
            _deltaPerVol[bar] = 100.0m * _cDelta[bar] / _cVolume[bar];

        _maxSessionDeltaPerVolume = Math.Max(Math.Abs(_deltaPerVol[bar]), _maxSessionDeltaPerVolume);

        if (_lastBar != bar)
            _lastVolumeValue = _lastDeltaValue = 0m;

        if (bar == CurrentBar - 1)
        {
            if (UseDeltaAlert && _lastDeltaAlert != bar)
            {
                if ((_lastDeltaValue < DeltaAlertValue && candle.Delta >= DeltaAlertValue)
                 || (_lastDeltaValue > DeltaAlertValue && candle.Delta <= DeltaAlertValue))
                {
                    AddAlert(DeltaAlertFile, $"Cluster statistic delta alert: {candle.Delta}");
                    _lastDeltaAlert = bar;
                }
            }

            if (UseVolumeAlert && _lastVolumeAlert != bar)
            {
                if (_lastVolumeValue < VolumeAlertValue && candle.Volume >= VolumeAlertValue)
                {
                    AddAlert(VolumeAlertFile, $"Cluster statistic volume alert: {candle.Volume}");
                    _lastVolumeAlert = bar;
                }
            }
        }

        _lastVolumeValue = candle.Volume;
        _lastDeltaValue = candle.Delta;
        _lastBar = bar;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is not { PriceChartContainer: { BarsWidth: > 2 } })
            return;

        if (LastVisibleBarNumber > CurrentBar - 1)
            return;

        var strCount = GetStrCount();

        if (strCount is 0)
            return;

        var bounds = context.ClipBounds;

        try
        {
            var renderField = new Rectangle(Container.Region.X, Container.Region.Y, Container.Region.Width,
                Container.Region.Height);
            context.SetClip(renderField);

            context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

            _height = Container.Region.Height / strCount;
            var overPixels = Container.Region.Height % strCount;

            var y = Container.Region.Y;

            var firstY = y;
            var linePen = new RenderPen(GridColor.Convert());
            var maxX = 0;
            var lastX = 0;

            var fullBarsWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
            var showHeaders = context.MeasureString("1", Font.RenderObject).Height * 0.8 <= _height;
            var showText = fullBarsWidth >= 30 && showHeaders;
            var textColor = TextColor.Convert();

            decimal maxVolumeSec;
            var maxDelta = 0m;
            var maxAsk = 0m;
            var maxBid = 0m;
            var maxMaxDelta = 0m;
            var maxMinDelta = 0m;
            var maxVolume = 0m;
            var cumVolume = 0m;
            var maxDeltaChange = 0m;
            var maxSessionDelta = 0m;
            var maxSessionDeltaPerVolume = 0m;
            var maxDeltaPerVolume = 0m;
            var minDelta = decimal.MaxValue;
            var maxHeight = 0m;
            var maxTicks = 0m;
            var maxDuration = 0m;

            if (VisibleProportion)
            {
                for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
                {
                    var candle = GetCandle(i);
                    maxDelta = Math.Max(candle.Delta, maxDelta);
                    maxVolume = Math.Max(candle.Volume, maxVolume);
                    minDelta = Math.Min(candle.MinDelta, minDelta);
                    maxAsk = Math.Max(candle.Ask, maxAsk);
                    maxBid = Math.Max(candle.Ask, maxBid);
                    maxMaxDelta = Math.Max(Math.Abs(candle.MaxDelta), maxMaxDelta);
                    maxMinDelta = Math.Max(Math.Abs(candle.MinDelta), maxMinDelta);
                    maxSessionDelta = Math.Max(Math.Abs(_cDelta[i]), maxSessionDelta);

                    if (candle.Volume is not 0)
                        maxDeltaPerVolume = Math.Max(Math.Abs(100 * candle.Delta / candle.Volume), maxDeltaPerVolume);
                    maxSessionDeltaPerVolume = Math.Max(Math.Abs(_deltaPerVol[i]), maxSessionDeltaPerVolume);
                    cumVolume += candle.Volume;

                    if (i == 0)
                        continue;

                    var prevCandle = GetCandle(i - 1);
                    maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), maxDeltaChange);
                    maxHeight = Math.Max(candle.High - candle.Low, maxHeight);
                    maxTicks = Math.Max(candle.Ticks, maxTicks);
                    maxDuration = Math.Max(_candleDurations[i], maxDuration);
                }

                maxVolumeSec = _volPerSecond.MAX(LastVisibleBarNumber - FirstVisibleBarNumber, LastVisibleBarNumber);
            }
            else
            {
                maxAsk = _maxAsk;
                maxBid = _maxBid;
                maxSessionDelta = _maxSessionDelta;
                maxDeltaPerVolume = _maxDeltaPerVolume;
                maxSessionDeltaPerVolume = _maxSessionDeltaPerVolume;
                maxDelta = _maxDelta;
                minDelta = _minDelta;
                maxMaxDelta = _maxMaxDelta;
                maxMinDelta = _maxMinDelta;
                maxVolume = _maxVolume;
                maxTicks = _maxTicks;
                maxDuration = _maxDuration;
                cumVolume = _cumVolume;
                maxDeltaChange = _maxDeltaChange;
                maxHeight = _maxHeight;
                maxVolumeSec = _volPerSecond.MAX(CurrentBar - 1, CurrentBar - 1);
            }

            for (var j = LastVisibleBarNumber; j >= FirstVisibleBarNumber; j--)
            {
                var x = ChartInfo.GetXByBar(j) + 3;

                maxX = Math.Max(x, maxX);

                var y1 = y;
                var candle = GetCandle(j);

                if (ShowAsk)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(candle.Ask, maxAsk);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.Ask);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowBid)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(candle.Bid, maxBid);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.Bid);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowDelta)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(Math.Abs(candle.Delta), maxDelta);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.Delta);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowDeltaPerVolume && candle.Volume != 0)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var deltaPerVol = 0m;

                    if (candle.Volume != 0)
                        deltaPerVol = candle.Delta * 100.0m / candle.Volume;

                    var rate = GetRate(Math.Abs(deltaPerVol), maxDeltaPerVolume);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = deltaPerVol.ToString("F") + "%";
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowSessionDelta)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(Math.Abs(_cDelta[j]), maxSessionDelta);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    bgBrush = Blend(_cDelta[j] > 0 ? AskColor : BidColor, _backGroundColor, rate);
                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(_cDelta[j]);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowSessionDeltaPerVolume)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(Math.Abs(_cDelta[j]), maxSessionDeltaPerVolume);
                    var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, _backGroundColor, rate);

                    bgBrush = Blend(_deltaPerVol[j] > 0 ? AskColor : BidColor, _backGroundColor, rate);
                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = _deltaPerVol[j].ToString("F") + "%";
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowMaximumDelta)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(Math.Abs(candle.MaxDelta), maxMaxDelta);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.MaxDelta);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowMinimumDelta)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(Math.Abs(candle.MinDelta), maxMinDelta);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.MinDelta);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowDeltaChange)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var prevCandle = GetCandle(Math.Max(j - 1, 0));
                    var change = candle.Delta - prevCandle.Delta;

                    var rectColor = change > 0 ? AskColor : BidColor;

                    var rate = GetRate(Math.Abs(change), maxDeltaChange);
                    var bgBrush = Blend(rectColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(change);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowVolume)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(candle.Volume, maxVolume);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(candle.Volume);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowVolumePerSecond)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(_volPerSecond[j], maxVolumeSec);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(_volPerSecond[j]);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowSessionVolume)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(_cVolume[j], cumVolume);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = ChartInfo.TryGetMinimizedVolumeString(_cVolume[j]);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowTicks)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(candle.Ticks, maxTicks);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = candle.Ticks.ToString(CultureInfo.InvariantCulture);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowHighLow)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(_candleHeights[j], maxHeight);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = _candleHeights[j].ToString(CultureInfo.InvariantCulture);
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowTime)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(_cVolume[j], cumVolume);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = candle.Time.AddHours(InstrumentInfo.TimeZone)
                            .ToString("HH:mm:ss");
                        rect.X += _headerOffset;
                        context.DrawString(s, Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                    overPixels--;
                }

                if (ShowDuration)
                {
                    var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                    var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

                    var rate = GetRate(_candleDurations[j], maxDuration);
                    var bgBrush = Blend(VolumeColor, _backGroundColor, rate);

                    context.FillRectangle(bgBrush, rect);

                    if (showText)
                    {
                        var s = (int)(candle.LastTime - candle.Time).TotalSeconds;
                        rect.X += _headerOffset;
                        context.DrawString($"{s}", Font.RenderObject, textColor, rect, _stringLeftFormat);
                    }

                    context.DrawLine(linePen, x, y1, x + fullBarsWidth, y1);
                    y1 += rectHeight;
                }

                context.DrawLine(linePen, x, y1 - 1, x + fullBarsWidth, y1 - 1);
                lastX = x + fullBarsWidth;
                context.DrawLine(linePen, lastX, Container.Region.Bottom, lastX, Container.Region.Y);
                overPixels = Container.Region.Height % strCount;
            }

            maxX += fullBarsWidth;

            if (HideRowsDescription && (MouseLocationInfo.LastPosition.Y < Container.Region.Y || MouseLocationInfo.LastPosition.Y > Container.Region.Bottom))
                return;

            var headBgBrush = HeaderBackground.Convert();

            if (ShowAsk)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Ask", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowBid)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Bid", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowDelta)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Delta", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowDeltaPerVolume)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Delta/Volume", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowSessionDelta)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Session Delta", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowSessionDeltaPerVolume)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Session Delta/Volume", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowMaximumDelta)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Max.Delta", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowMinimumDelta)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Min.Delta", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowDeltaChange)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Delta Change", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowVolume)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Volume", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowVolumePerSecond)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Volume/sec", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowSessionVolume)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Session Volume", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowTicks)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Trades", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowHighLow)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Height", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowTime)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Time", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                overPixels--;
                context.DrawLine(linePen, Container.Region.X, y, lastX, y);
            }

            if (ShowDuration)
            {
                var rectHeight = _height + (overPixels > 0 ? 1 : 0);
                var descRect = new Rectangle(0, y, _headerWidth, rectHeight);
                context.FillRectangle(headBgBrush, descRect);
                context.DrawRectangle(linePen, descRect);

                if (showHeaders)
                {
                    descRect.X += _headerOffset;
                    context.DrawString("Duration", Font.RenderObject, textColor, descRect, _stringLeftFormat);
                }

                y += rectHeight;
                context.DrawLine(linePen, Container.Region.X, y, _headerWidth, y);
            }

            context.DrawLine(linePen, 0, Container.Region.Bottom - 1, maxX, Container.Region.Bottom - 1);
            context.DrawLine(linePen, 0, firstY - y, maxX, firstY - y);
        }
        catch (ArgumentOutOfRangeException)
        {
            //Chart cleared
            return;
        }
        catch (Exception e)
        {
            this.LogError("Cluster statistic rendering error ", e);
            throw;
        }
        finally
        {
            context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
            context.SetClip(bounds);
        }
    }

    #endregion

    #region Private methods

    private int GetStrCount()
    {
        var height = 0;

        if (ShowAsk)
            height++;

        if (ShowBid)
            height++;

        if (ShowDelta)
            height++;

        if (ShowSessionDelta)
            height++;

        if (ShowSessionDeltaPerVolume)
            height++;

        if (ShowSessionVolume)
            height++;

        if (ShowVolumePerSecond)
            height++;

        if (ShowVolume)
            height++;

        if (ShowDeltaPerVolume)
            height++;

        if (ShowTicks)
            height++;

        if (ShowHighLow)
            height++;

        if (ShowTime)
            height++;

        if (ShowMaximumDelta)
            height++;

        if (ShowMinimumDelta)
            height++;

        if (ShowDeltaChange)
            height++;

        if (ShowDuration)
            height++;
        return height;
    }

    private decimal GetRate(decimal value, decimal maximumValue)
    {
        if (maximumValue == 0)
            return 10;

        var rate = value * 100.0m / (maximumValue * 0.6m);

        if (rate < 10)
            rate = 10;

        if (rate > 100)
            return 100;

        return rate;
    }

    private System.Drawing.Color Blend(Color color, Color backColor, decimal amount)
    {
        var r = (byte)(color.R + (backColor.R - color.R) * (1 - amount * 0.01m));
        var g = (byte)(color.G + (backColor.G - color.G) * (1 - amount * 0.01m));
        var b = (byte)(color.B + (backColor.B - color.B) * (1 - amount * 0.01m));
        return System.Drawing.Color.FromArgb(255, r, g, b);
    }

    #endregion
}