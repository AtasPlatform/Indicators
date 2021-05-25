namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	[DisplayName("Fractals")]
	public class Fractals : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _fractalDown = new("Fractal Down");
		private readonly ValueDataSeries _fractalUp = new("Fractal Up");

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
				var bar0 = GetCandle(bar);
				var bar1 = GetCandle(bar - 1);
				var bar2 = GetCandle(bar - 2);
				var bar3 = GetCandle(bar - 3);
				var bar4 = GetCandle(bar - 4);

				if (bar2.High > bar3.High && bar2.High > bar4.High && bar2.High > bar1.High && bar2.High > bar0.High)
					_fractalUp[bar - 2] = bar2.High + 3 * _tickSize;
				else
					_fractalUp[bar - 2] = 0;

				if (bar2.Low < bar3.Low && bar2.Low < bar4.Low && bar2.Low < bar1.Low && bar2.Low < bar0.Low)
					_fractalDown[bar - 2] = bar2.Low - 3 * _tickSize;
				else
					_fractalDown[bar - 2] = 0;
			}
		}

		#endregion
	}
}