namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Inside or Equals Bar")]
	public class InsideEqualsBar : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "InsideBar")]
			Inside,

			[Display(ResourceType = typeof(Resources), Name = "InsideEqualBar")]
			InsideEqual
		}

		#endregion

		#region Fields

		private readonly PaintbarsDataSeries _renderSeries = new PaintbarsDataSeries("PaintBars");
		private Mode _calcMode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "InsideBar", GroupName = "Colors", Order = 200)]
		public Color TrueColor { get; set; } = Colors.Blue;

		[Display(ResourceType = typeof(Resources), Name = "Bars", GroupName = "Colors", Order = 210)]
		public Color FakeColor { get; set; } = Colors.Red;

		#endregion

		#region ctor

		public InsideEqualsBar()
			: base(true)
		{
			_renderSeries.IsHidden = true;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			switch (_calcMode)
			{
				case Mode.Inside:
					if (candle.High < prevCandle.High && candle.Low > prevCandle.Low)
						_renderSeries[bar] = TrueColor;
					else
						_renderSeries[bar] = FakeColor;
					break;
				case Mode.InsideEqual:
					if (candle.High <= prevCandle.High && candle.Low >= prevCandle.Low)
						_renderSeries[bar] = TrueColor;
					else
						_renderSeries[bar] = FakeColor;
					break;
			}
		}

		#endregion
	}
}