using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	public interface IPpsAttachmentItem
	{
		bool Remove();
	} // interface IPpsAttachmentItem

	public interface IPpsAttachments : IEnumerable<IPpsAttachmentItem>
	{
	} // interface IPpsAttachments

	public interface IPpsAttachmentSource
	{
		IPpsAttachments Attachments { get; }
	} // interface IPpsAttachmentSource

	public class PpsAttachmentsControl : Control, IPpsAttachmentSource
	{
		public PpsAttachmentsControl()
		{
		} // ctor

		public IPpsAttachments Attachments { get; } = null; // wird von ItemsSource abgeleitet
	} // class PpsAttachmentsControl
}
