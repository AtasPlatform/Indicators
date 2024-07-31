﻿namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context.GDIPlus;
    using OFT.Rendering.Settings;

    using Utils.Common.Collections;

    using Pen = System.Drawing.Pen;

    [DisplayName("Fractals")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.FractalsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602388")]
	public class Fractals : Indicator
	{
		#region Nested types

		public enum ShowMode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.High))]
			High,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low))]
			Low,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
			All,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
			None
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _fractalDown = new("FractalDown", "Fractal Down")
		{
			VisualType = VisualMode.Dots,
			ShowZeroValue = false,
			Width = 5
		};

		private readonly ValueDataSeries _fractalUp = new("FractalUp", "Fractal Up")
		{
			Color = System.Drawing.Color.LimeGreen.Convert(),
			VisualType = VisualMode.Dots,
			ShowZeroValue = false,
			Width = 5
		};

		private Pen _highPen;
		private Pen _lowPen;
		private ShowMode _mode = ShowMode.All;
        private bool _showLine;
		private decimal _tickSize;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Line), Description = nameof(Strings.IsNeedShowLinesDescription), Order = 100)]
		public bool ShowLine
		{
			get => _showLine;
			set
			{
				_showLine = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.High), GroupName = nameof(Strings.Line), Description = nameof(Strings.PenSettingsDescription), Order = 110)]
		public PenSettings HighPen { get; set; } = new() { Color = System.Drawing.Color.LimeGreen.Convert() };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low), GroupName = nameof(Strings.Line), Description = nameof(Strings.PenSettingsDescription), Order = 120)]
		public PenSettings LowPen { get; set; } = new() { Color = DefaultColors.Red.Convert() };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisualModeDescription), Order = 200)]
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
			_highPen = HighPen.RenderObject.ToPen();
			_lowPen = LowPen.RenderObject.ToPen();

			HighPen.PropertyChanged += HighPenChanged;
			LowPen.PropertyChanged += LowPenChanged;

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

			if (bar < 4)
				return;


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

		#endregion

		#region Private methods

		private void HighPenChanged(object sender, PropertyChangedEventArgs e)
		{
			var highPen = HighPen.RenderObject.ToPen();

			HorizontalLinesTillTouch
				.Where(x => x.Pen == _highPen)
				.ToList()
				.ForEach(x => x.Pen = highPen);
			_highPen = highPen;
		}

		private void LowPenChanged(object sender, PropertyChangedEventArgs e)
		{
			var lowPen = LowPen.RenderObject.ToPen();

			HorizontalLinesTillTouch
				.Where(x => x.Pen == _lowPen)
				.ToList()
				.ForEach(x => x.Pen = lowPen);
			_lowPen = lowPen;
		}

		#endregion
	}
}