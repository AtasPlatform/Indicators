namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Collections;

	using Color = System.Drawing.Color;
	using Pen = System.Drawing.Pen;

	[DisplayName("Fractals")]
	public class Fractals : Indicator
	{
		public enum ShowMode
		{
			[Display(ResourceType = typeof(Resources), Name = "High")]
			High,
			[Display(ResourceType = typeof(Resources), Name = "Low")]
			Low,
			[Display(ResourceType = typeof(Resources), Name = "Any")]
			All,
			[Display(ResourceType = typeof(Resources), Name = "None")]
			None
		}

		#region Fields

		private readonly ValueDataSeries _fractalDown = new("Fractal Down");
		private readonly ValueDataSeries _fractalUp = new("Fractal Up");
		private readonly Pen _highPen = new(Color.Green);
		private readonly Pen _lowPen = new(Color.Red);
		private bool _showLine;
		private decimal _tickSize;
		private ShowMode _mode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "Line", Order = 100)]
		public bool ShowLine
		{
			get => _showLine;
			set
			{
				_showLine = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 200)]
		public ShowMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Fractals()
			: base(true)
		{
			DenyToChangePanel = true;

			_mode = ShowMode.All;

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
				HorizontalLinesTillTouch.Clear();
			}

			if (Mode is ShowMode.None)
				return;

			if (bar >= 4)
			{
				var bar0 = GetCandle(bar);
				var bar1 = GetCandle(bar - 1);
				var bar2 = GetCandle(bar - 2);
				var bar3 = GetCandle(bar - 3);
				var bar4 = GetCandle(bar - 4);

				if (bar2.High > bar3.High && bar2.High > bar4.High && bar2.High > bar1.High && bar2.High > bar0.High && Mode is ShowMode.High or ShowMode.All)
				{
					_fractalUp[bar - 2] = bar2.High + 3 * _tickSize;

					if (ShowLine)
						HorizontalLinesTillTouch.Add(new LineTillTouch(bar - 2, bar2.High, _highPen));
				}
				else
				{
					_fractalUp[bar - 2] = 0;

					if (ShowLine)
						HorizontalLinesTillTouch.RemoveWhere(x => x.FirstBar == bar - 2 && x.Pen == _highPen);
				}

				if (bar2.Low < bar3.Low && bar2.Low < bar4.Low && bar2.Low < bar1.Low && bar2.Low < bar0.Low && Mode is ShowMode.Low or ShowMode.All)
				{
					_fractalDown[bar - 2] = bar2.Low - 3 * _tickSize;

					if (ShowLine)
						HorizontalLinesTillTouch.Add(new LineTillTouch(bar - 2, bar2.Low, _lowPen));
				}
				else
				{
					_fractalDown[bar - 2] = 0;

					if (ShowLine)
						HorizontalLinesTillTouch.RemoveWhere(x => x.FirstBar == bar - 2 && x.Pen == _lowPen);
				}
			}
		}

		#endregion
	}
}