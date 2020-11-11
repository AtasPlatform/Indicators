namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("BollingerBands")]
	[LocalizedDescription(typeof(Resources), "BollingerBands")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/6724-bollingerbands")]
	public class BollingerBands : Indicator
	{
		#region Fields

		private readonly RangeDataSeries _band = new RangeDataSeries("Background");
		private readonly StdDev _dev = new StdDev();

		private readonly SMA _sma = new SMA();
		private decimal _width;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = _dev.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "BBandsWidth",
			GroupName = "Common",
			Order = 22)]
		public decimal Width
		{
			get => _width;
			set
			{
				if (value <= 0)
					return;

				_width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBands()
		{
			((ValueDataSeries)DataSeries[0]).Color = Colors.Green;
			DataSeries[0].Name = "Bollinger Bands";

			DataSeries.Add(new ValueDataSeries("Up")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(new ValueDataSeries("Down")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(_band);
			Period = 10;
			Width = 1;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sma = _sma.Calculate(bar, value);
			var dev = _dev.Calculate(bar, value);

			this[bar] = sma;

			DataSeries[1][bar] = sma + dev * Width;
			DataSeries[2][bar] = sma - dev * Width;

			_band[bar].Upper = sma + dev * Width;
			_band[bar].Lower = sma - dev * Width;
		}

		#endregion
	}
}