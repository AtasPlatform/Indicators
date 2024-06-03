namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Camarilla Pivots")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CamarillaPivotsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602341")]
	public class CamarillaPivots : Indicator
	{
		#region Fields

		private decimal _close;
		private decimal _high;
		private decimal _low;
		private decimal _lastH1;
		private decimal _lastH2;
		private decimal _lastH3;
		private decimal _lastH4;
		private decimal _lastH5;
		private decimal _lastH6;
		
		private decimal _lastL1;
		private decimal _lastL2;
		private decimal _lastL3;
		private decimal _lastL4;
		private decimal _lastL5;
		private decimal _lastL6;

		private decimal _lastPivot;
		
		
		private ValueDataSeries _h1 = new("H1", "Daily H1") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };
        private ValueDataSeries _h2 = new("H2", "Daily H2") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };
        private ValueDataSeries _h3 = new("H3", "Daily H3") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };
        private ValueDataSeries _h4 = new("H4", "Daily H4") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };
        private ValueDataSeries _h5 = new("H5", "Daily H5") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };
        private ValueDataSeries _h6 = new("H6", "Daily H6") { DescriptionKey = nameof(Strings.ResistanceLevelDisplayDescription) };

        private ValueDataSeries _l1 = new("L1", "Daily L1") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
        private ValueDataSeries _l2 = new("L2", "Daily L2") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
        private ValueDataSeries _l3 = new("L3", "Daily L3") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
        private ValueDataSeries _l4 = new("L4", "Daily L4") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
		private ValueDataSeries _l5 = new("L5", "Daily L5") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
        private ValueDataSeries _l6 = new("L6", "Daily L6") { DescriptionKey = nameof(Strings.SupportLevelDisplayDescription) };
        private int _lastSession;

		private ValueDataSeries _pivot = new("Pivot", "Daily Pivot") { DescriptionKey = nameof(Strings.HLCAveragePrevSessionDescription) };
		private int _lastBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PivotRange), GroupName = nameof(Strings.Colors), Description = nameof(Strings.PivotLevelColorDescription), Order = 100)]
		public CrossColor PivotColor
		{
			get => _pivot.Color;
			set => _pivot.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BetweenColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.MidLevelColorDescription), Order = 100)]
		public CrossColor BetweenColor
		{
			get => _h3.Color;
			set => _h3.Color = _l3.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLowColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.AllLevelsColorDescription), Order = 110)]
		public CrossColor HighLowColor
		{
			get => _h1.Color;
			set => _h1.Color = _h2.Color = _h4.Color = _h5.Color = _h6.Color =
				_l1.Color = _l2.Color = _l4.Color = _l5.Color = _l6.Color = value;
		}

		#endregion

		#region ctor

		public CamarillaPivots()
			: base(true)
		{
			DenyToChangePanel = true;
			PivotColor = DefaultColors.Gray.Convert();
			BetweenColor = DefaultColors.Red.Convert();
			HighLowColor = DefaultColors.Green.Convert();

			_pivot.ScaleIt = false;
			_h6.ScaleIt = false;
            _h5.ScaleIt = false;
            _h4.ScaleIt = false;
            _h3.ScaleIt = false;
            _h2.ScaleIt = false;
            _h1.ScaleIt = false;
            _l1.ScaleIt = false;
            _l2.ScaleIt = false;
            _l3.ScaleIt = false;
            _l4.ScaleIt = false;
            _l5.ScaleIt = false;
            _l6.ScaleIt = false;

            DataSeries[0] = _pivot;
			DataSeries.Add(_h6);
			DataSeries.Add(_h5);
			DataSeries.Add(_h4);
			DataSeries.Add(_h3);
			DataSeries.Add(_h2);
			DataSeries.Add(_h1);
			DataSeries.Add(_l1);
			DataSeries.Add(_l2);
			DataSeries.Add(_l3);
			DataSeries.Add(_l4);
			DataSeries.Add(_l5);
			DataSeries.Add(_l6);

			DataSeries.ForEach(x => ((ValueDataSeries)x).Width = 2);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_close = _high = _low = 0;
				_lastH1 = _lastH2 = _lastH3 = _lastH4 = _lastH5 = _lastH6 = 0;
				_lastL1 = _lastL2 = _lastL3 = _lastL4 = _lastL5 = _lastL6 = 0;
				_lastSession = 0;
			}

			var candle = GetCandle(bar);

			if (IsNewSession(bar) && _lastSession != bar)
			{
				DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(bar - 1));
								
				_lastSession = bar;
				
				_lastPivot = (_high + _low + _close) / 3;
				var range = _high - _low;
				_lastH5 = _high / _low * _close;
				_lastH4 = _close + range * 1.1m / 2;
				_lastH3 = _close + range * 1.1m / 4;
				_lastH2 = _close + range * 1.1m / 6;
				_lastH1 = _close + range * 1.1m / 12;
                _lastH6 = _lastH5 + 1.168m * (_lastH5 - _lastH4);

                _lastL1 = _close - range * 1.1m / 12;
				_lastL2 = _close - range * 1.1m / 6;
				_lastL3 = _close - range * 1.1m / 4;
				_lastL4 = _close - range * 1.1m / 2;
				_lastL5 = _close - (_lastH5 - _close);
				_lastL6 = _close - (_lastH6 - _close);

				_close = _high = _low = 0;
			}

			_close = candle.Close;

			if (candle.High > _high || _high == 0)
				_high = candle.High;

			if (candle.Low < _low || _low == 0)
				_low = candle.Low;

			if (_lastBar == bar)
				return; 
			
			RenderValues(bar);

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private void RenderValues(int bar)
		{
			if (bar == 0)
				return;

			for (var i = _lastSession; i <= bar; i++)
			{
				_pivot[i] = _lastPivot;
				_h6[i] = _lastH6;
				_h5[i] = _lastH5;
				_h4[i] = _lastH4;
				_h3[i] = _lastH3;
				_h2[i] = _lastH2;
				_h1[i] = _lastH1;
				_l1[i] = _lastL1;
				_l2[i] = _lastL2;
				_l3[i] = _lastL3;
				_l4[i] = _lastL4;
				_l5[i] = _lastL5;
				_l6[i] = _lastL6;
			}
		}

		#endregion
	}
}