namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Reflection;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[DisplayName("Maximum Levels")]
	[Category("Clusters, Profiles, Levels")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MaxLevelsIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602426")]
    public class MaxLevels : Indicator
	{
		#region Nested types

		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum MaxLevelType
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
			Bid,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
			Ask,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveDelta))]
			PositiveDelta,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeDelta))]
			NegativeDelta,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
			Volume,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Tick,

			[Obsolete]
			[Browsable(false)]
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Time))]
			Time
		}

		#endregion

		#region Fields

		private RenderFont _axisFont = new("Arial", 11F, FontStyle.Regular, GraphicsUnit.Point, 204);
		private Color _axisTextColor = System.Drawing.Color.White;
		private IndicatorCandle _candle;
		private bool _candleRequested;
		private string _description = "Current Day";
		private RenderFont _font = new("Arial", 10);
		private int _lastAlert;
		private int _lastSession;
		private Color _lineColor = System.Drawing.Color.CornflowerBlue;
		private FixedProfilePeriods _period = FixedProfilePeriods.CurrentDay;
		private decimal _prevClose;
		private RenderPen _renderPen = new(System.Drawing.Color.CornflowerBlue, 2);

		private RenderStringFormat _stringRightFormat = new()
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private Color _textColor = System.Drawing.Color.Black;
		private int _width = 2;

        #endregion

        #region Properties

        #region Calculation

        [Parameter]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.Period), Description = nameof(Strings.ProfilePeriodDescription), Order = 10)]
		public FixedProfilePeriods Period
		{
			get => _period;
			set
			{
				_period = value;
				_description = GetPeriodDescription(_period);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.Type), Description = nameof(Strings.SourceTypeDescription), Order = 20)]
		public MaxLevelType Type { get; set; } = MaxLevelType.Volume;

        #endregion

        #region Visualization

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.Color), Description = nameof(Strings.LineColorDescription), Order = 30)]
		public System.Windows.Media.Color Color
		{
			get => _lineColor.Convert();
			set
			{
				_lineColor = value.Convert();
				_renderPen = new RenderPen(_lineColor, _width);
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.Width), Description = nameof(Strings.LineWidthDescription), Order = 40)]
		[Range(1, 100)]
		public int Width
		{
			get => _width;
			set
			{
				_width = value;
				_renderPen = new RenderPen(_lineColor, _width);
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.Length), Description = nameof(Strings.LineLengthDescription), Order = 45)]
		[Range(1, 10000)]
		public int Length { get; set; } = 300;

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.AxisTextColor), Description = nameof(Strings.AxisTextColorDescription), Order = 50)]
		public System.Windows.Media.Color AxisTextColor
		{
			get => _axisTextColor.Convert();
			set => _axisTextColor = value.Convert();
		}

        #endregion

        #region Label

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.LabelOffset), Description = nameof(Strings.LabelOffsetXDescription), Order = 51)]
		[Range(0, 10000)]
        public int LabelOffset { get; set; }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.Text), Description = nameof(Strings.IsNeedShowLabelDescription), Order = 52)]
		public bool ShowText { get; set; } = true;

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.CustomLabel), Description = nameof(Strings.LabelTextDescription), Order = 54)]
		public String CustomLabel { get; set; }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.Value), Description = nameof(Strings.ShowValueOnLabelDescription), Order = 55)]
		public bool ShowValue { get; set; }

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.Color), Description = nameof(Strings.LabelTextColorDescription), Order = 60)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

        [Range(7, 100)]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Label), Name = nameof(Strings.Size), Description = nameof(Strings.FontSizeDescription), Order = 70)]
		public int FontSize
		{
			get => (int)_font.Size;
			set => _font = new RenderFont("Arial", value);
		}

        #endregion

        #region Alerts

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertDescription), Order = 100)]
		public bool UseAlert { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 110)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription), Order = 120)]
		public System.Windows.Media.Color AlertForeColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription), Order = 130)]
		public System.Windows.Media.Color AlertBgColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 75, 72, 72);

        #endregion

        #endregion

        #region ctor

        public MaxLevels()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			DrawAbovePrice = true;
		}

		#endregion

		#region Protected methods
		
		protected override void OnApplyDefaultColors()
		{
			if (ChartInfo is null)
				return;

			AxisTextColor = ChartInfo.ColorsStore.AxisTextColor.Convert();
			TextColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor.Convert();
        }
		
		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_lastAlert = 0;
				_prevClose = GetCandle(CurrentBar - 1).Close;
			}

			if (bar == 0 || IsNewSession(bar) && _lastSession != bar)
				_candleRequested = false;

			if (bar != CurrentBar - 1)
				return;

			if (!_candleRequested)
			{
				_candleRequested = true;
				GetFixedProfile(new FixedProfileRequest(Period));
				_lastSession = bar;
			}

			var candle = GetCandle(bar);

			if (UseAlert && _lastAlert != bar && _candle is not null)
			{
				var priceInfo = GetPriceVolumeInfo(_candle, Type);

				if (candle.Close >= priceInfo.Price && _prevClose < priceInfo.Price
				    ||
				    candle.Close <= priceInfo.Price && _prevClose > priceInfo.Price)
				{
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached maximum level: {priceInfo.Price}", AlertBgColor, AlertForeColor);
					_lastAlert = bar;
				}
			}

			_prevClose = candle.Close;
		}

		protected override void OnFixedProfilesResponse(IndicatorCandle fixedProfileScaled, IndicatorCandle fixedProfileOriginScale, FixedProfilePeriods period)
		{
			_candle = fixedProfileOriginScale;
			RedrawChart();
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if(ChartInfo is null || InstrumentInfo is null)
				return;

			if (_candle == null)
				return;

			var priceInfo = GetPriceVolumeInfo(_candle, Type);

			if (priceInfo == null)
				return;

			var y = ChartInfo.GetYByPrice(priceInfo.Price, false);
			var firstX = ChartInfo.PriceChartContainer.Region.Width - Length;
			var secondX = ChartInfo.PriceChartContainer.Region.Width;

			context.DrawLine(_renderPen, firstX, y, secondX, y);

			this.DrawLabelOnPriceAxis(context, string.Format(ChartInfo.StringFormat, priceInfo.Price), y, _axisFont, _lineColor, _axisTextColor);

			if (!ShowText && !ShowValue)
				return;

			var renderText = "";

			if (ShowText)
				renderText += string.IsNullOrWhiteSpace(CustomLabel) ? _description : CustomLabel;

            if (ShowValue)
			{
				var value = Type switch
				{
					MaxLevelType.Bid => priceInfo.Bid,
					MaxLevelType.Ask => priceInfo.Ask,
					MaxLevelType.PositiveDelta => priceInfo.Ask - priceInfo.Bid,
					MaxLevelType.NegativeDelta => priceInfo.Ask - priceInfo.Bid,
					MaxLevelType.Tick => priceInfo.Ticks,
					MaxLevelType.Time => priceInfo.Time,
					_ => priceInfo.Volume
				};

				var stringValue = CutValue(value);

				if (ShowText)
					renderText += " " + stringValue;
			}

			var size = context.MeasureString(renderText, _font);

			var textRect = new Rectangle(new Point(ChartInfo.PriceChartContainer.Region.Width - size.Width - 20 - LabelOffset, y - size.Height - Width / 2),
				new Size(size.Width + 20, size.Height));

			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);
			context.DrawString(renderText, _font, _textColor, textRect, _stringRightFormat);
			context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
		}

		#endregion

		#region Private methods

		private string CutValue(decimal value)
		{
			var kValue = value / 1000;
			var mValue = value / 1000000;

			if (kValue < 1)
				return $"{value:0.##}";

			return mValue < 1
				? $"{kValue:0.##}K"
				: $"{mValue:0.##}M";
		}

		private string GetPeriodDescription(FixedProfilePeriods period)
		{
			return period switch
			{
				FixedProfilePeriods.CurrentDay => "Current day",
				FixedProfilePeriods.LastDay => "Last day",
				FixedProfilePeriods.CurrentWeek => "Current week",
				FixedProfilePeriods.LastWeek => "Last week",
				FixedProfilePeriods.CurrentMonth => "Current month",
				FixedProfilePeriods.LastMonth => "Last month",
				FixedProfilePeriods.Contract => "Contract",
				_ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
			};
		}

		private PriceVolumeInfo GetPriceVolumeInfo(IndicatorCandle candle, MaxLevelType levelType)
		{
			return Type switch
			{
				MaxLevelType.Bid => _candle.MaxBidPriceInfo,
				MaxLevelType.Ask => _candle.MaxAskPriceInfo,
				MaxLevelType.PositiveDelta => _candle.MaxPositiveDeltaPriceInfo,
				MaxLevelType.NegativeDelta => _candle.MaxNegativeDeltaPriceInfo,
				MaxLevelType.Volume => _candle.MaxVolumePriceInfo,
				MaxLevelType.Tick => _candle.MaxTickPriceInfo,
				MaxLevelType.Time => _candle.MaxTimePriceInfo,
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		#endregion
	}
}