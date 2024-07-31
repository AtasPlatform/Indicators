namespace ATAS.Indicators.Technical
{
	using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
	
    [DisplayName("Ratio")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.RatioDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602282")]
	public class Ratio : Indicator
	{
		#region Nested types

		private class RatioSign
		{
			#region Fields

			public readonly int Bar;
			public readonly int Direction;
			public readonly decimal Price;
			public readonly decimal Ratio;

			#endregion

			#region ctor

			public RatioSign(int bar, int direction, decimal ratio, decimal price)
			{
				Bar = bar;
				Direction = direction;
				Ratio = ratio;
				Price = price;
			}

			#endregion
		}

		#endregion

		#region Static and constants

		public const int Call = 1;
		public const int Put = -1;
		public const int Wait = 0;

		#endregion

		#region Fields
		
		private CrossColor _bgColor = DefaultColors.Yellow.Convert();
		private int _days = 20;
        private int _fontSize = 10;
        private CrossColor _highColor = DefaultColors.Blue.Convert();
		private CrossColor _lowColor = DefaultColors.Green.Convert();
		private decimal _lowRatio = 0.71m;
		private CrossColor _neutralColor = DefaultColors.Gray.Convert();
		private decimal _neutralRatio = 29m;
		private int _targetBar;
		public int CallPutCount;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.LowRatio), Description = nameof(Strings.LowRatioDescription), Order = 20)]
        public decimal LowRatio
        {
            get => _lowRatio;
            set
            {
                _lowRatio = value;
                ReDraw();
            }
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.NeutralRatio), Description = nameof(Strings.HighRatioDescription), Order = 21)]

        public decimal NeutralRatio
        {
            get => _neutralRatio;
            set
            {
                _neutralRatio = value;
                ReDraw();
            }
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        [Range(0, 1000)]
        public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.LowColor), Description = nameof(Strings.LowRatioColorDescription), Order = 10)]
		public CrossColor LowColor
		{
			get => _lowColor;
			set
			{
				_lowColor = value;
				ReDraw();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.NeutralColor), Description = nameof(Strings.NeutralRatioColorDescription), Order = 11)]
		public CrossColor NeutralColor
		{
			get => _neutralColor;
			set
			{
				_neutralColor = value;
				ReDraw();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.HighColor), Description = nameof(Strings.HighRatioColorDescription), Order = 12)]
		public CrossColor HighColor
		{
			get => _highColor;
			set
			{
				_highColor = value;
				ReDraw();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.BackGround), Description = nameof(Strings.FillColorDescription), Order = 13)]
		public CrossColor BackgroundColor
		{
			get => _bgColor;
			set
			{
				_bgColor = value;
				ReDraw();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.FontSize), Description = nameof(Strings.FontSizeDescription), Order = 22)]
		[Range(1, 10000)]
        public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = value;
				ReDraw();
			}
		}

		#endregion

		#region ctor

		public Ratio()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_targetBar = 0;

				if (_days <= 0)
					return;

				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}

				return;
			}

			if (bar < _targetBar)
				return;

			var rs = CaclulateRatio(bar);
			AddLabel(rs);
		}

		#endregion

		#region Private methods

		private void ReDraw()
		{
			try
			{
				foreach (var l in Labels)
				{
					decimal ratio = 0;

					if (l.Value.Text.Length > 0)
						ratio = Convert.ToDecimal(l.Value.Text);
					l.Value.AutoSize = _fontSize == 0;
					l.Value.FontSize = _fontSize;
					l.Value.XOffset = 0;
					l.Value.FontSize = FontSize;
					l.Value.FillColor = System.Drawing.Color.FromArgb(_bgColor.A, _bgColor.R, _bgColor.G, _bgColor.B);

					if (ratio <= _lowRatio)
						l.Value.Textcolor = System.Drawing.Color.FromArgb(_lowColor.A, _lowColor.R, _lowColor.G, _lowColor.B);
					else if (ratio <= _neutralRatio)
						l.Value.Textcolor = System.Drawing.Color.FromArgb(_neutralColor.A, _neutralColor.R, _neutralColor.G, _neutralColor.B);
					else
						l.Value.Textcolor = System.Drawing.Color.FromArgb(_highColor.A, _highColor.R, _highColor.G, _highColor.B);
				}
			}
			catch (Exception)
			{
			}
		}

		private RatioSign CaclulateRatio(int bar)
		{
			RatioSign rs;
			var candle = GetCandle(bar);

			if (candle.Open < candle.Close) // bullish
			{
				var lowBid = 0;
				var lowBid2 = 0;
				var volumeInfo = candle.GetPriceVolumeInfo(candle.Low);

				if (volumeInfo != null)
					lowBid = (int)volumeInfo.Bid;

				var volumeInfo2 = candle.GetPriceVolumeInfo(candle.Low + InstrumentInfo.TickSize);

				if (volumeInfo2 != null)
					lowBid2 = (int)volumeInfo2.Bid;
				decimal ratio = 0;

				if (lowBid > 0)
					ratio = (decimal)lowBid2 / lowBid;
				rs = new RatioSign(bar, Call, ratio, candle.Low - 4 * InstrumentInfo.TickSize);
			}
			else if (candle.Open > candle.Close) // bearish
			{
				var highAsk = 0;
				var highAsk2 = 0;

				var volumeInfo = candle.GetPriceVolumeInfo(candle.High);

				if (volumeInfo != null)
					highAsk = (int)volumeInfo.Ask;

				var volumeInfo2 = candle.GetPriceVolumeInfo(candle.High - InstrumentInfo.TickSize);

				if (volumeInfo2 != null)
					highAsk2 = (int)volumeInfo2.Ask;
				decimal ratio = 0;

				if (highAsk > 0)
					ratio = (decimal)highAsk2 / highAsk;
				rs = new RatioSign(bar, Put, ratio, candle.High + 2 * InstrumentInfo.TickSize);
			}
			else
				rs = new RatioSign(bar, 0, 0, 0);

			return rs;
		}

		private void AddLabel(RatioSign rs)
		{
			var bg = System.Drawing.Color.FromArgb(_bgColor.A, _bgColor.R, _bgColor.G, _bgColor.B);
			var price = rs.Price;
			var labelName = "BAR_" + rs.Bar;

			if (Labels.Count > 0)
			{
				var lastLabel = Labels.Last();

				if (lastLabel.Key.Equals(labelName))
					Labels.Remove(lastLabel.Key);
			}

			var sRatio = rs.Ratio.ToString("N2");
			sRatio = sRatio.Replace(",00", "");

			if (rs.Direction == Call)
			{
				if (rs.Ratio <= _lowRatio)
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_lowColor.A, _lowColor.R, _lowColor.G, _lowColor.B), System.Drawing.Color.Transparent, bg, _fontSize,
						DrawingText.TextAlign.Center, _fontSize == 0);
				}
				else if (rs.Ratio <= _neutralRatio)
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_neutralColor.A, _neutralColor.R, _neutralColor.G, _neutralColor.B)
						, System.Drawing.Color.Transparent, bg, _fontSize,
						DrawingText.TextAlign.Center, _fontSize == 0);
				}
				else
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_highColor.A, _highColor.R, _highColor.G, _highColor.B)
						, System.Drawing.Color.Transparent, bg, _fontSize,
						DrawingText.TextAlign.Center, _fontSize == 0);
				}
			}
			else if (rs.Direction == Put)
			{
				if (rs.Ratio <= _lowRatio)
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_lowColor.A, _lowColor.R, _lowColor.G, _lowColor.B)
						, System.Drawing.Color.Transparent, bg, _fontSize, DrawingText.TextAlign.Center, _fontSize == 0);
				}
				else if (rs.Ratio <= _neutralRatio)
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_neutralColor.A, _neutralColor.R, _neutralColor.G, _neutralColor.B)
						, System.Drawing.Color.Transparent, bg, _fontSize,
						DrawingText.TextAlign.Center, _fontSize == 0);
				}
				else
				{
					AddText(labelName, sRatio, true, rs.Bar, price, 0, 0,
						System.Drawing.Color.FromArgb(_highColor.A, _highColor.R, _highColor.G, _highColor.B)
						, System.Drawing.Color.Transparent, bg, _fontSize,
						DrawingText.TextAlign.Center, _fontSize == 0);
				}
			}
			else
			{
				AddText(labelName, "", true, rs.Bar, price, 0, 0,
					System.Drawing.Color.FromArgb(_lowColor.A, _lowColor.R, _lowColor.G, _lowColor.B)
					, System.Drawing.Color.Transparent, bg, _fontSize,
					DrawingText.TextAlign.Center, _fontSize == 0);
			}
		}

		#endregion
	}
}