namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Logging;

	[DisplayName("Cumulative Delta")]
	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/412-cumulative-delta")]
	public class CumulativeDelta : Indicator
	{
		#region Nested types

		[Serializable]
		public enum SessionDeltaVisualMode
		{
			[Display(ResourceType = typeof(Resources), Name = "Candles")]
			Candles = 0,

			[Display(ResourceType = typeof(Resources), Name = "Bars")]
			Bars = 1,

			[Display(ResourceType = typeof(Resources), Name = "Line")]
			Line = 2
		}

		#endregion

		#region Fields

		private decimal _cumDelta;
		private decimal _high;
		private decimal _low;

		private SessionDeltaVisualMode _mode = SessionDeltaVisualMode.Candles;
		private decimal _open;

		private bool _sessionDeltaMode = true;
		private bool _subscribedTochangeZeroLine;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "VisualMode")]
		public SessionDeltaVisualMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				if (_mode == SessionDeltaVisualMode.Candles)
				{
					((CandleDataSeries)DataSeries[1]).Visible = true;
					((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.OnlyValueOnAxis;
					((ValueDataSeries)DataSeries[2]).VisualType = VisualMode.Hide;
					((ValueDataSeries)DataSeries[3]).VisualType = VisualMode.Hide;
				}
				else
				{
					((CandleDataSeries)DataSeries[1]).Visible = false;
					if (_mode == SessionDeltaVisualMode.Line)
					{
						((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Line;
						((ValueDataSeries)DataSeries[2]).VisualType = VisualMode.Hide;
						((ValueDataSeries)DataSeries[3]).VisualType = VisualMode.Hide;
					}
					else
					{
						((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
						((ValueDataSeries)DataSeries[2]).VisualType = VisualMode.Histogram;
						((ValueDataSeries)DataSeries[3]).VisualType = VisualMode.Histogram;
					}
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SessionDeltaMode")]
		public bool SessionDeltaMode
		{
			get => _sessionDeltaMode;
			set
			{
				_sessionDeltaMode = value;
				RaisePropertyChanged("SessionDeltaMode");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseScale")]
		public bool UseScale
		{
			get => LineSeries[0].UseScale;
			set => LineSeries[0].UseScale = value;
		}

		#endregion

        #region Overrides of Indicator

        public CumulativeDelta()
			: base(true)
		{
			LineSeries.Add(new LineSeries("Zero") { Color = Colors.Gray, Width = 1 });
			var series = (ValueDataSeries)DataSeries[0];
			series.Width = 2;
			series.Color = Colors.Green;
			DataSeries.Add(new CandleDataSeries("Candles"));
			DataSeries.Add(new ValueDataSeries("Positive Histogram") { Color = Colors.Green });
			DataSeries.Add(new ValueDataSeries("Negative Histogram") { Color = Colors.Red });
			Panel = IndicatorDataProvider.NewPanel;
			Mode = Mode;
		}

		protected override void OnCalculate(int i, decimal value)
		{
			if (!_subscribedTochangeZeroLine)
			{
				_subscribedTochangeZeroLine = true;

				LineSeries[0].PropertyChanged += (sender, arg) =>
				{
					if (arg.PropertyName == "UseScale")
						RecalculateValues();
				};
			}

			if (i == 0)
				_cumDelta = 0;

			try
			{
				var newSession = false;

				if (i > CurrentBar)
					return;

				if (SessionDeltaMode && i > 0 && IsNewSession(i))
				{
					_open = _cumDelta = _high = _low = 0;
					newSession = true;
				}

				if (newSession)
					((ValueDataSeries)DataSeries[0]).SetPointOfEndLine(i - 1);

				var currentCandle = GetCandle(i);

				if (i == 0 || newSession)
					_cumDelta += currentCandle.Ask - currentCandle.Bid;
				else
				{
					if (SessionDeltaMode && i > 0 && IsNewSession(i))
					{
						_open = 0;
						_low = currentCandle.MinDelta;
						_high = currentCandle.MaxDelta;
						_cumDelta = currentCandle.Delta;
					}
					else
					{
						var prev = (decimal)DataSeries[0][i - 1];
						_open = prev;
						_cumDelta = prev + currentCandle.Delta;
						var dh = currentCandle.MaxDelta - currentCandle.Delta;
						var dl = currentCandle.Delta - currentCandle.MinDelta;
						_low = _cumDelta - dl;
						_high = _cumDelta + dh;
					}
				}

				DataSeries[0][i] = _cumDelta;

				if (_cumDelta >= 0)
				{
					DataSeries[2][i] = _cumDelta;
					DataSeries[3][i] = 0m;
				}
				else
				{
					DataSeries[2][i] = 0m;
					DataSeries[3][i] = _cumDelta;
				}

				DataSeries[1][i] = new Candle
				{
					Close = _cumDelta,
					High = _high,
					Low = _low,
					Open = _open
				};
			}
			catch (Exception exc)
			{
				this.LogError("CumulativeDelta calculation error", exc);
			}
		}

		#endregion
	}
}