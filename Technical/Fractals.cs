namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	[DisplayName("Fractals")]
	public class Fractals : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _fractalDown = new ValueDataSeries("FractalDown");
		private readonly ValueDataSeries _fractalUp = new ValueDataSeries("FractalUp");

		private decimal _tickSize;

		#endregion

		#region ctor

		public Fractals()
			: base(true)
		{
			DenyToChangePanel = true;

			_fractalUp.VisualType = VisualMode.Dots;
			_fractalUp.ShowZeroValue = false;
			_fractalUp.Width = 5;

			_fractalDown.VisualType = VisualMode.Dots;
			_fractalDown.ShowZeroValue = false;
			_fractalDown.Width = 5;

			_fractalUp.Color = Colors.LimeGreen;
			_fractalDown.Color = Colors.Red;

			DataSeries[0] = _fractalUp;
			DataSeries.Add(_fractalDown);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_tickSize = ChartInfo.PriceChartContainer.Step;
				DataSeries.ForEach(x => x.Clear());
			}

			if (bar >= 4)
			{
				if (GetCandle(bar - 2).High > GetCandle(bar - 3).High && GetCandle(bar - 2).High > GetCandle(bar - 4).High
					&&
					GetCandle(bar - 2).High > GetCandle(bar - 1).High && GetCandle(bar - 2).High > GetCandle(bar).High)
					_fractalUp[bar - 2] = GetCandle(bar - 2).High + 3 * _tickSize;
				else
					_fractalUp[bar - 2] = 0;

				if (GetCandle(bar - 2).Low < GetCandle(bar - 3).Low && GetCandle(bar - 2).Low < GetCandle(bar - 4).Low
					&&
					GetCandle(bar - 2).Low < GetCandle(bar - 1).Low && GetCandle(bar - 2).Low < GetCandle(bar).Low)
				{
					_fractalDown[bar - 2] = GetCandle(bar - 2).Low - 3 * _tickSize;
					;
				}
				else
					_fractalDown[bar - 2] = 0;
			}
		}

		#endregion
	}
}