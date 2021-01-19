namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Settings;

	[DisplayName("Round Numbers")]
	public class RoundNr : Indicator
	{
		#region Fields

		private Color _lineColor;

		private int _step;
		private LineDashStyle _style;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Step", GroupName = "Settings", Order = 100)]
		public int Step
		{
			get => _step;
			set
			{
				if (value <= 0)
					return;

				_step = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DashStyle", GroupName = "Settings", Order = 110)]
		public LineDashStyle Style
		{
			get => _style;
			set
			{
				_style = value;
				LineSeries.ForEach(x => x.LineDashStyle = value);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Settings", Order = 120)]
		public Color LineColor
		{
			get => _lineColor;
			set
			{
				_lineColor = value;
				LineSeries.ForEach(x => x.Color = value);
			}
		}

		#endregion

		#region ctor

		public RoundNr()
			: base(true)
		{
			DenyToChangePanel = true;
			_step = 100;
			_style = LineDashStyle.Solid;
			DataSeries[0].IsHidden = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				LineSeries.Clear();

				for (var i = GetFirstValue(candle.Low); i <= candle.High; i += _step * 0.01m)
					AddLine(i);

				return;
			}

			var maxLine = LineSeries.Max(x => x.Value);
			var minLine = LineSeries.Min(x => x.Value);

			if (candle.High > maxLine)
			{
				for (var i = maxLine; i <= candle.High; i += _step * 0.01m)
					AddLine(i);
			}

			if (candle.Low > minLine)
				return;

			for (var i = minLine; i >= candle.Low; i -= _step * 0.01m)
				AddLine(i);
		}

		#endregion

		#region Private methods

		private decimal GetFirstValue(decimal low)
		{
			var lowLines = low / (_step * 0.01m);

			if (lowLines % 1 == 0)
				return low;

			return Math.Truncate(lowLines) * _step * 0.01m;
		}

		private void AddLine(decimal value)
		{
			LineSeries.Add(
				new LineSeries($"{value}")
				{
					Value = value,
					Width = 1,
					IsHidden = true,
					Text = $"{value:0.##}",
					LineDashStyle = _style,
					Color = _lineColor
				});
		}

		#endregion
	}
}