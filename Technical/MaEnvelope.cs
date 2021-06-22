namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Moving Average Envelope")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45340-moving-average-envelope")]
	public class MaEnvelope : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "FixedValue")]
			FixedValue,

			[Display(ResourceType = typeof(Resources), Name = "Percent")]
			Percentage
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _botSeries = new(Resources.BottomBand);
		private readonly SMA _sma = new();
		private readonly ValueDataSeries _smaSeries = new(Resources.MiddleBand);

		private readonly ValueDataSeries _topSeries = new(Resources.TopBand);
		private Mode _calcMode;
		private decimal _value;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Mode", GroupName = "Settings", Order = 110)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Settings", Order = 110)]
		public decimal Value
		{
			get => _value;
			set
			{
				if (value <= 0)
					return;

				_value = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MaEnvelope()
		{
			_sma.Period = 10;
			_calcMode = Mode.Percentage;
			_value = 1;

			_botSeries.Color = _topSeries.Color = Colors.Blue;
			_smaSeries.Color = Colors.Red;

			DataSeries[0] = _botSeries;
			DataSeries.Add(_topSeries);
			DataSeries.Add(_smaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_smaSeries[bar] = _sma[bar];

			if (_calcMode == Mode.FixedValue)
			{
				_topSeries[bar] = _sma[bar] + _value;
				_botSeries[bar] = _sma[bar] - _value;
			}
			else
			{
				_topSeries[bar] = _sma[bar] * (1 + 0.01m * _value);
				_botSeries[bar] = _sma[bar] * (1 - 0.01m * _value);
			}
		}

		#endregion
	}
}