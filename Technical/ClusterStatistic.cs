namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using Color = System.Windows.Media.Color;

[DisplayName("Cluster Statistic")]
[Category("Clusters, Profiles, Levels")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/454-cluster-statistic")]
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

	private readonly RenderStringFormat _stringLeftFormat = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly ValueDataSeries _volPerSecond = new("VolPerSecond");
	private Color _backGroundColor;
	private bool _centerAlign;
	private decimal _cumVolume;

	private int _height = 15;
	private int _lastDeltaAlert;
	private decimal _lastDeltaValue;
	private int _lastVolumeAlert;
	private decimal _lastVolumeValue;
	private decimal _maxDelta;
	private decimal _maxDeltaChange;
	private decimal _maxHeight;
	private decimal _maxVolume;
	private decimal _minDelta;
	private decimal _maxTicks;
	private decimal _maxDuration;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "HeaderBackground", GroupName = "Colors", Order = 11)]
	public Color HeaderBackground { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Colors", Order = 12)]
	public Color BackGroundColor
	{
		get => _backGroundColor;
		set => _backGroundColor = Color.FromArgb(120, value.R, value.G, value.B);
	}

	[Display(ResourceType = typeof(Resources), Name = "AskColor", GroupName = "Colors", Order = 13)]
	public Color AskColor { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "BidColor", GroupName = "Colors", Order = 14)]
	public Color BidColor { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "VolumeColor", GroupName = "Colors", Order = 15)]
	public Color VolumeColor { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Colors", Order = 16)]
	public Color TextColor { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "GridColor", GroupName = "Colors", Order = 17)]
	public Color GridColor { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "VisibleProportion", GroupName = "Settings", Order = 100)]
	public bool VisibleProportion { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Settings", Order = 100)]
	public FontSetting Font { get; set; } = new("Arial", 9);

	[Display(ResourceType = typeof(Resources), Name = "ShowAsk", GroupName = "Strings", Order = 110)]
	public bool ShowAsk { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowBid", GroupName = "Strings", Order = 110)]
	public bool ShowBid { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowDelta", GroupName = "Strings", Order = 120)]
	public bool ShowDelta { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowDeltaPerVolume", GroupName = "Strings", Order = 130)]
	public bool ShowDeltaPerVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowSessionDelta", GroupName = "Strings", Order = 140)]
	public bool ShowSessionDelta { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowSessionDeltaPerVolume", GroupName = "Strings", Order = 150)]
	public bool ShowSessionDeltaPerVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowMaximumDelta", GroupName = "Strings", Order = 160)]
	public bool ShowMaximumDelta { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowMinimumDelta", GroupName = "Strings", Order = 170)]
	public bool ShowMinimumDelta { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowDeltaChange", GroupName = "Strings", Order = 175)]
	public bool ShowDeltaChange { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Strings", Order = 180)]
	public bool ShowVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowVolumePerSecond", GroupName = "Strings", Order = 190)]
	public bool ShowVolumePerSecond { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowSessionVolume", GroupName = "Strings", Order = 191)]
	public bool ShowSessionVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowTradesCount", GroupName = "Strings", Order = 192)]
	public bool ShowTicks { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowHeight", GroupName = "Strings", Order = 193)]
	public bool ShowHighLow { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowTime", GroupName = "Strings", Order = 194)]
	public bool ShowTime { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ShowDuration", GroupName = "Strings", Order = 196)]
	public bool ShowDuration { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "HideRowsDescription", GroupName = "Visualization", Order = 200)]
	public bool HideRowsDescription { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "CenterAlign", GroupName = "Visualization", Order = 210)]
	public bool CenterAlign
	{
		get => _centerAlign;
		set
		{
			_centerAlign = value;
			_stringLeftFormat.Alignment = value ? StringAlignment.Center : StringAlignment.Near;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "ShortValues", GroupName = "ShortValues", Order = 300)]
	public bool ShortValues { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "DigitsAfterComma", GroupName = "ShortValues", Order = 310)]
	[Range(0, 100)]
	public int DecimalDigits { get; set; } = 2;

	[Display(ResourceType = typeof(Resources), Name = "Volume", GroupName = "Alerts", Order = 400)]
	public bool UseVolumeAlert { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Alerts", Order = 410)]
	[Range(0, 100000000)]
	public decimal VolumeAlertValue { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 420)]
	public string VolumeAlertFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Resources), Name = "Delta", GroupName = "Alerts", Order = 430)]
	public bool UseDeltaAlert { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Alerts", Order = 440)]
	public decimal DeltaAlertValue { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 450)]
	public string DeltaAlertFile { get; set; } = "alert1";

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
		_backGroundColor = Colors.Black;
		AskColor = Colors.Green;
		BidColor = Colors.Red;
		TextColor = Colors.White;
		GridColor = Colors.Transparent;
		VolumeColor = Colors.DarkGray;
		HeaderBackground = Color.FromRgb(84, 84, 84);
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		ShowDescription = false;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		if (bar == 0)
		{
			_cumVolume = 0;
			_maxVolume = 0;
			_maxDelta = 0;
			_maxDeltaChange = 0;
			_minDelta = decimal.MaxValue;
			_maxHeight = 0;
			_maxTicks = 0;
			_maxDuration = 0;
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

		_maxDeltaChange = Math.Max(Math.Abs(candle.Delta - prevCandle.Delta), _maxDeltaChange);

		_maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

		_maxVolume = Math.Max(candle.Volume, _maxVolume);

		_minDelta = Math.Min(candle.MinDelta, _minDelta);

		var candleHeight = candle.High - candle.Low;
		_maxHeight = Math.Max(candleHeight, _maxHeight);
		_candleHeights[bar] = candleHeight;

		_maxTicks = Math.Max(candle.Ticks, _maxTicks);

		_candleDurations[bar] = (int)(candle.LastTime - candle.Time).TotalSeconds;
		_maxDuration = Math.Max(_candleDurations[bar], _maxDuration);

		if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
			_deltaPerVol[bar] = 100.0m * _cDelta[bar] / _cVolume[bar];

		_volPerSecond[bar] = candle.Volume / Math.Max(1, Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds));

		if (!UseDeltaAlert || !UseVolumeAlert || bar != CurrentBar - 1)
			return;

		if (UseDeltaAlert && _lastDeltaAlert != bar)
		{
			if ((_lastDeltaValue < DeltaAlertValue && candle.Delta >= DeltaAlertValue)
			    ||
			    (_lastDeltaValue > DeltaAlertValue && candle.Delta <= DeltaAlertValue))
			{
				AddAlert(DeltaAlertFile, $"Cluster statistic delta alert: {candle.Delta}");
				_lastDeltaAlert = bar;
			}
		}

		if (UseVolumeAlert && _lastVolumeAlert != bar)
		{
			if ((_lastVolumeValue < VolumeAlertValue && candle.Volume >= VolumeAlertValue)
			    ||
			    (_lastVolumeValue > VolumeAlertValue && candle.Volume <= VolumeAlertValue))
			{
				AddAlert(VolumeAlertFile, $"Cluster statistic volume alert: {candle.Volume}");
				_lastVolumeAlert = bar;
			}
		}

		_lastVolumeValue = candle.Volume;
		_lastDeltaValue = candle.Delta;
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo==null|| ChartInfo.PriceChartContainer==null|| ChartInfo.PriceChartContainer.BarsWidth < 3)
			return;

		var bounds = context.ClipBounds;

		try
		{
			var renderField = new Rectangle(Container.Region.X, Container.Region.Y, Container.Region.Width,
				Container.Region.Height);
			context.SetClip(renderField);

			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

			var strCount = GetStrCount();

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
			var maxVolume = 0m;
			var cumVolume = 0m;
			var maxDeltaChange = 0m;
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
				maxDelta = _maxDelta;
				minDelta = _minDelta;
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
				var rate = GetRate(Math.Abs(candle.Delta), maxDelta);
				var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate);

				if (ShowAsk)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.Ask) : $"{candle.Ask:0.##}";
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

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.Bid) : $"{candle.Bid:0.##}";
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

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.Delta) : $"{candle.Delta:0.##}";
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
					bgBrush = Blend(_cDelta[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(_cDelta[j]) : $"{_cDelta[j]:0.##}";
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
					bgBrush = Blend(_deltaPerVol[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
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

					rate = GetRate(candle.Volume, maxVolume);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.MaxDelta) : $"{candle.MaxDelta:0.##}";
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

					rate = GetRate(candle.MinDelta, minDelta);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.MinDelta) : $"{candle.MinDelta:0.##}";
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
					rate = GetRate(Math.Abs(change), maxDeltaChange);

					var rectColor = change > 0 ? AskColor : BidColor;
					bgBrush = Blend(rectColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText && j > 0)
					{
						var s = ShortValues ? CutValue(candle.Delta - prevCandle.Delta) : $"{candle.Delta - prevCandle.Delta:0.##}";
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

					rate = GetRate(candle.Volume, maxVolume);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(candle.Volume) : $"{candle.Volume:0.##}";
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

					rate = GetRate(_volPerSecond[j], maxVolumeSec);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(_volPerSecond[j]) : $"{_volPerSecond[j]:0.##}";
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

					rate = GetRate(_cVolume[j], cumVolume);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush, rect);

					if (showText)
					{
						var s = ShortValues ? CutValue(_cVolume[j]) : $"{_cVolume[j]:0.##}";
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

					rate = GetRate(candle.Ticks, maxTicks);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

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

					rate = GetRate(_candleHeights[j], maxHeight);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

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

					rate = GetRate(_cVolume[j], cumVolume);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

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

					rate = GetRate(_candleDurations[j], maxDuration);
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

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

			if (HideRowsDescription)
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

	private string CutValue(decimal value)
	{
		var kValue = value / 1000;

		if (Math.Abs(kValue) < 1)
			return $"{value.ToString($"N{DecimalDigits}")}";

		var mValue = kValue / 1000;

		return Math.Abs(mValue) < 1
			? $"{kValue.ToString($"N{DecimalDigits}")}K"
			: $"{mValue.ToString($"N{DecimalDigits}")}M";
	}

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