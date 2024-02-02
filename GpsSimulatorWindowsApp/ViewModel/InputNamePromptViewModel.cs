using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class InputNamePromptViewModel : ObservableObject
	{
		private string _title;
		private string _nameLabel;
		private string? _positiveButtonLabel;
		private string? _negativeButtonLabel;
		private string? _inputValue;
		private Func<string?, string?> _validationAction;
		private Action _positiveAction;
		private Action _negativeAction;

		public InputNamePromptViewModel(
			string title,
			string nameLabel,
			string? initialValue,
			string? positiveButtonLabel,
			string? negativeButtonLabel,
			Func<string?, string?> validationAction,
			Action positiveAction,
			Action negativeAction)
		{
			_title = title;
			_nameLabel = nameLabel;
			_inputValue = initialValue;
			_positiveButtonLabel = positiveButtonLabel;
			_negativeButtonLabel = negativeButtonLabel;
			_validationAction = validationAction;
			_positiveAction = positiveAction;
			_negativeAction = negativeAction;

			PositiveButtonCommand = new RelayCommand(ApplyPositiveAction);
			NegativeButtonCommand = new RelayCommand(ApplyNegativeAction);
		}

		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value, nameof(Title));
		}
		
		public string NameLabel
		{
			get => _nameLabel;
			set => SetProperty(ref _nameLabel, value, nameof(NameLabel));
		}

		public string? PositiveButtonLabel
		{
			get => _positiveButtonLabel;
			set => SetProperty(ref _positiveButtonLabel, value, nameof(PositiveButtonLabel));
		}

		public string? NegativeButtonLabel
		{
			get => _negativeButtonLabel;
			set => SetProperty(ref _negativeButtonLabel, value, nameof(NegativeButtonLabel));
		}

		public string? InputValue
		{
			get => _inputValue;
			set => SetProperty(ref _inputValue, value, nameof(InputValue));
		}

		public IRelayCommand PositiveButtonCommand { get; private set; }

		public IRelayCommand NegativeButtonCommand { get; private set; }

		private void ApplyPositiveAction()
		{
			if (_validationAction != null)
			{
				var errorMessage = _validationAction(InputValue);
				if (!string.IsNullOrEmpty(errorMessage))
				{
					MessageBox.Show(errorMessage, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}

			_positiveAction?.Invoke();
		}

		private void ApplyNegativeAction()
		{
			_negativeAction?.Invoke();
		}
	}
}
