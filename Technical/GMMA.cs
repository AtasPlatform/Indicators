namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Guppy Multiple Moving Average")]
	public class GMMA : Indicator
	{
		#region Fields

		private readonly EMA _emaLong1 = new EMA();
		private readonly EMA _emaLong2 = new EMA();
		private readonly EMA _emaLong3 = new EMA();
		private readonly EMA _emaLong4 = new EMA();
		private readonly EMA _emaLong5 = new EMA();
		private readonly EMA _emaLong6 = new EMA();

		private readonly EMA _emaShort1 = new EMA();
		private readonly EMA _emaShort2 = new EMA();
		private readonly EMA _emaShort3 = new EMA();
		private readonly EMA _emaShort4 = new EMA();
		private readonly EMA _emaShort5 = new EMA();
		private readonly EMA _emaShort6 = new EMA();

		private readonly ValueDataSeries _renderLong1 = new ValueDataSeries("Long1");
		private readonly ValueDataSeries _renderLong2 = new ValueDataSeries("Long2");
		private readonly ValueDataSeries _renderLong3 = new ValueDataSeries("Long3");
		private readonly ValueDataSeries _renderLong4 = new ValueDataSeries("Long4");
		private readonly ValueDataSeries _renderLong5 = new ValueDataSeries("Long5");
		private readonly ValueDataSeries _renderLong6 = new ValueDataSeries("Long6");

		private readonly ValueDataSeries _renderShort1 = new ValueDataSeries("Short1");
		private readonly ValueDataSeries _renderShort2 = new ValueDataSeries("Short2");
		private readonly ValueDataSeries _renderShort3 = new ValueDataSeries("Short3");
		private readonly ValueDataSeries _renderShort4 = new ValueDataSeries("Short4");
		private readonly ValueDataSeries _renderShort5 = new ValueDataSeries("Short5");
		private readonly ValueDataSeries _renderShort6 = new ValueDataSeries("Short6");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Colors", Order = 50)]
		public Color ShortColor
		{
			get => _renderShort1.Color;
			set =>
				_renderShort1.Color = _renderShort2.Color = _renderShort3.Color =
					_renderShort4.Color = _renderShort5.Color = _renderShort6.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Colors", Order = 60)]
		public Color LongColor
		{
			get => _renderLong1.Color;
			set =>
				_renderLong1.Color = _renderLong2.Color = _renderLong3.Color =
					_renderLong4.Color = _renderLong5.Color = _renderLong6.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod1", GroupName = "ShortPeriod", Order = 100)]
		public int EmaPeriod1
		{
			get => _emaShort1.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort1.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod2", GroupName = "ShortPeriod", Order = 110)]
		public int EmaPeriod2
		{
			get => _emaShort2.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort2.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod3", GroupName = "ShortPeriod", Order = 120)]
		public int EmaPeriod3
		{
			get => _emaShort3.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort3.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod4", GroupName = "ShortPeriod", Order = 130)]
		public int EmaPeriod4
		{
			get => _emaShort4.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort4.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod5", GroupName = "ShortPeriod", Order = 140)]
		public int EmaPeriod5
		{
			get => _emaShort5.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort5.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod6", GroupName = "ShortPeriod", Order = 150)]
		public int EmaPeriod6
		{
			get => _emaShort6.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort6.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod1", GroupName = "LongPeriod", Order = 200)]
		public int EmaLongPeriod1
		{
			get => _emaLong1.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong1.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod2", GroupName = "LongPeriod", Order = 210)]
		public int EmaLongPeriod2
		{
			get => _emaLong2.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong2.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod3", GroupName = "LongPeriod", Order = 220)]
		public int EmaLongPeriod3
		{
			get => _emaLong3.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong3.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod4", GroupName = "LongPeriod", Order = 230)]
		public int EmaLongPeriod4
		{
			get => _emaLong4.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong4.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod5", GroupName = "LongPeriod", Order = 240)]
		public int EmaLongPeriod5
		{
			get => _emaLong5.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong5.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod6", GroupName = "LongPeriod", Order = 250)]
		public int EmaLongPeriod6
		{
			get => _emaLong6.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong6.Period = value;

				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public GMMA()
		{
			DenyToChangePanel = true;

			LongColor = Colors.Red;
			ShortColor = Colors.Blue;

			_emaShort1.Period = 3;
			_emaShort2.Period = 5;
			_emaShort3.Period = 7;
			_emaShort4.Period = 10;
			_emaShort5.Period = 12;
			_emaShort6.Period = 15;

			_emaLong1.Period = 30;
			_emaLong2.Period = 35;
			_emaLong3.Period = 40;
			_emaLong4.Period = 45;
			_emaLong5.Period = 50;
			_emaLong6.Period = 60;

			DataSeries[0] = _renderShort1;
			DataSeries.Add(_renderShort2);
			DataSeries.Add(_renderShort3);
			DataSeries.Add(_renderShort4);
			DataSeries.Add(_renderShort5);
			DataSeries.Add(_renderShort6);

			DataSeries.Add(_renderLong1);
			DataSeries.Add(_renderLong2);
			DataSeries.Add(_renderLong3);
			DataSeries.Add(_renderLong4);
			DataSeries.Add(_renderLong5);
			DataSeries.Add(_renderLong6);

			DataSeries.ForEach(x => x.IsHidden = true);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderShort1[bar] = _emaShort1.Calculate(bar, value);
			_renderShort2[bar] = _emaShort2.Calculate(bar, value);
			_renderShort3[bar] = _emaShort3.Calculate(bar, value);
			_renderShort4[bar] = _emaShort4.Calculate(bar, value);
			_renderShort5[bar] = _emaShort5.Calculate(bar, value);
			_renderShort6[bar] = _emaShort6.Calculate(bar, value);

			_renderLong1[bar] = _emaLong1.Calculate(bar, value);
			_renderLong2[bar] = _emaLong2.Calculate(bar, value);
			_renderLong3[bar] = _emaLong3.Calculate(bar, value);
			_renderLong4[bar] = _emaLong4.Calculate(bar, value);
			_renderLong5[bar] = _emaLong5.Calculate(bar, value);
			_renderLong6[bar] = _emaLong6.Calculate(bar, value);
		}

		#endregion
	}
}