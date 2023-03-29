namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Moving Average Envelope")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45340-moving-average-envelope")]
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

        private readonly SMA _sma = new() { Period = 10 };

        private readonly ValueDataSeries _botSeries = new(Resources.BottomBand)
        {
	        Color = DefaultColors.Blue.Convert(),
			IgnoredByAlerts = true
        };
        private readonly ValueDataSeries _smaSeries = new(Resources.MiddleBand);
        private readonly ValueDataSeries _topSeries = new(Resources.TopBand)
        {
	        Color = DefaultColors.Blue.Convert(),
			IgnoredByAlerts = true
        };

        private Mode _calcMode = Mode.Percentage;
        private decimal _value = 1;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _sma.Period;
			set
			{
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

		[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Settings", Order = 120)]
		[Range(0.00001, 10000)]
        public decimal Value
		{
			get => _value;
			set
			{
				_value = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MaEnvelope()
		{
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