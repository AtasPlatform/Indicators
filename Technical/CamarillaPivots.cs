namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Camarilla Pivots")]
	[FeatureId("Not Ready")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45991-camarilla-pivots")]
	public class CamarillaPivots : Indicator
	{
		#region Fields

		private decimal _close;
		private ValueDataSeries _h1 = new("Daily H1");
		private ValueDataSeries _h2 = new("Daily H2");
		private ValueDataSeries _h3 = new("Daily H3");
		private ValueDataSeries _h4 = new("Daily H4");
		private ValueDataSeries _h5 = new("Daily H5");
		private ValueDataSeries _h6 = new("Daily H6");
		private decimal _high;
		private ValueDataSeries _l1 = new("Daily L1");
		private ValueDataSeries _l2 = new("Daily L2");
		private ValueDataSeries _l3 = new("Daily L3");
		private ValueDataSeries _l4 = new("Daily L4");
		private ValueDataSeries _l5 = new("Daily L5");
		private ValueDataSeries _l6 = new("Daily L6");
		private int _lastSession;
		private decimal _low;

		private ValueDataSeries _pivot = new("Daily Pivot");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "PivotRange", GroupName = "Colors", Order = 100)]
		public Color PivotColor
		{
			get => _pivot.Color;
			set => _pivot.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "BetweenColor", GroupName = "Colors", Order = 100)]
		public Color BetweenColor
		{
			get => _h3.Color;
			set => _h3.Color = _l3.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "HighLowColor", GroupName = "Colors", Order = 110)]
		public Color HighLowColor
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
			PivotColor = Colors.Gray;
			BetweenColor = Colors.Red;
			HighLowColor = Colors.Green;

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
				_lastSession = -1;
			}

			var candle = GetCandle(bar);

			if (IsNewSession(bar) && _lastSession != bar)
			{
				DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(bar - 1));

				_close = _high = _low = 0;
				_lastSession = bar;
			}

			_close = candle.Close;

			if (candle.High > _high || _high == 0)
				_high = candle.High;

			if (candle.Low < _low || _low == 0)
				_low = candle.Low;

			RenderValues(bar);
		}

		#endregion

		#region Private methods

		private void RenderValues(int bar)
		{
			if (bar != 0)
			{
				var pivot = (_high + _low + _close) / 3;
				var range = _high - _low;
				var h5 = _high / _low * _close;
				var h4 = _close + range * 1.1m / 2;
				var h3 = _close + range * 1.1m / 4;
				var h2 = _close + range * 1.1m / 6;
				var h1 = _close + range * 1.1m / 12;

				var l1 = _close - range * 1.1m / 12;
				var l2 = _close - range * 1.1m / 6;
				var l3 = _close - range * 1.1m / 4;
				var l4 = _close - range * 1.1m / 2;
				var h6 = h5 + 1.168m * (h5 - h4);

				var l5 = _close - (h5 - _close);
				var l6 = _close - (h6 - _close);

				for (var i = _lastSession; i <= bar; i++)
				{
					_pivot[i] = pivot;
					_h6[i] = h6;
					_h5[i] = h5;
					_h4[i] = h4;
					_h3[i] = h3;
					_h2[i] = h2;
					_h1[i] = h1;
					_l1[i] = l1;
					_l2[i] = l2;
					_l3[i] = l3;
					_l4[i] = l4;
					_l5[i] = l5;
					_l6[i] = l6;
				}
			}
		}

		#endregion
	}
}