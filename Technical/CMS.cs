namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Clear Method Swing Line")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45489-clear-method-swing-line")]
	public class CMS : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downSeries = new(Resources.Down);

		private readonly ValueDataSeries _hh = new("hh");

		private readonly ValueDataSeries _hh1 = new("hh1");

		private readonly ValueDataSeries _hh2 = new("hh2");

		private readonly ValueDataSeries _hh3 = new("hh3");
		private readonly ValueDataSeries _hl = new("hl");
		private readonly ValueDataSeries _hl1 = new("hl1");
		private readonly ValueDataSeries _hl2 = new("hl2");
		private readonly ValueDataSeries _hl3 = new("hl3");
		private readonly ValueDataSeries _lh = new("lh");
		private readonly ValueDataSeries _lh1 = new("lh1");
		private readonly ValueDataSeries _lh2 = new("lh2");
		private readonly ValueDataSeries _lh3 = new("lh3");
		private readonly ValueDataSeries _ll = new("ll");
		private readonly ValueDataSeries _ll1 = new("ll1");
		private readonly ValueDataSeries _ll2 = new("ll2");
		private readonly ValueDataSeries _ll3 = new("ll3");

		private readonly ValueDataSeries _upSeries = new(Resources.Up);
		private readonly ValueDataSeries _us = new("us");
		private readonly ValueDataSeries _us1 = new("us1");
		private readonly ValueDataSeries _us2 = new("us2");
		private readonly ValueDataSeries _us3 = new("us3");

		#endregion

		#region ctor

		public CMS()
			: base(true)
		{
			DenyToChangePanel = true;
			_upSeries.Color = Colors.Cyan;
			_downSeries.Color = Colors.Magenta;

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_hh1[bar] = _hh2[bar] = _hh3[bar] = _hh[bar] = candle.High;
				_hl1[bar] = _hl2[bar] = _hl3[bar] = _hl[bar] = candle.Low;
				_us1[bar] = _us2[bar] = _us3[bar] = _us[bar] = 1;
				_ll1[bar] = _ll2[bar] = _ll3[bar] = _ll[bar] = candle.Low;
				_lh1[bar] = _lh2[bar] = _lh3[bar] = _lh[bar] = candle.High;

				DataSeries.ForEach(x =>
				{
					x.Clear();
					((ValueDataSeries)x).SetPointOfEndLine(0);
				});
				return;
			}

			_hh1[bar] = candle.High > _hh[bar - 1] ? candle.High : _hh[bar - 1];
			_hl1[bar] = candle.Low > _hl[bar - 1] ? candle.Low : _hl[bar - 1];
			_us1[bar] = candle.High < _hl1[bar] ? 0 : 1;
			_ll1[bar] = candle.High < _hl1[bar] ? candle.Low : _ll[bar - 1];
			_lh1[bar] = candle.High < _hl1[bar] ? candle.High : _lh[bar - 1];

			_ll2[bar] = candle.Low < _ll1[bar] ? candle.Low : _ll1[bar];
			_lh2[bar] = candle.High < _lh1[bar] ? candle.High : _lh1[bar];
			_us2[bar] = candle.Low > _lh2[bar] ? 1 : 0;
			_hh2[bar] = candle.Low > _lh2[bar] ? candle.High : _hh1[bar];
			_hl2[bar] = candle.Low > _lh2[bar] ? candle.Low : _hl1[bar];

			_ll3[bar] = candle.Low < _ll[bar - 1] ? candle.Low : _ll[bar - 1];
			_lh3[bar] = candle.High < _lh[bar - 1] ? candle.High : _lh[bar - 1];
			_us3[bar] = candle.Low > _lh3[bar] ? 1 : 0;
			_hh3[bar] = candle.Low > _lh3[bar] ? candle.High : _hh[bar - 1];
			_hl3[bar] = candle.Low > _lh3[bar] ? candle.Low : _lh[bar - 1];

			if (_us[bar - 1] == 0)
			{
				_hh[bar] = _hh3[bar];
				_hl[bar] = _hl3[bar];
				_us[bar] = _us3[bar];
				_ll[bar] = _ll3[bar];
				_lh[bar] = _lh3[bar];
			}
			else
			{
				if (_us1[bar] == 0)
				{
					_hh[bar] = _hh2[bar];
					_hl[bar] = _hl2[bar];
					_us[bar] = _us2[bar];
					_ll[bar] = _ll2[bar];
					_lh[bar] = _lh2[bar];
				}
				else
				{
					_hh[bar] = _hh1[bar];
					_hl[bar] = _hl1[bar];
					_us[bar] = _us1[bar];
					_ll[bar] = _ll1[bar];
					_lh[bar] = _lh1[bar];
				}
			}

			if (_us[bar] == 1)
				_upSeries[bar] = _hl[bar];
			else
				_downSeries[bar] = _lh[bar];

			if (_upSeries[bar] != 0 && _downSeries[bar - 1] != 0 || _downSeries[bar] != 0 && _upSeries[bar - 1] != 0)
				SplitLines(bar);
		}

		#endregion

		#region Private methods

		private void SplitLines(int bar)
		{
			if (_upSeries[bar] != 0 && _downSeries[bar - 1] != 0)
			{
				_upSeries[bar - 1] = _downSeries[bar - 1];
				_upSeries.SetPointOfEndLine(bar - 2);
				_downSeries.SetPointOfEndLine(bar - 1);
			}

			if (_downSeries[bar] != 0 && _upSeries[bar - 1] != 0)
			{
				_downSeries[bar - 1] = _upSeries[bar - 1];
				_downSeries.SetPointOfEndLine(bar - 2);
				_upSeries.SetPointOfEndLine(bar - 1);
			}
		}

		#endregion
	}
}