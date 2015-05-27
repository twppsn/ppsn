using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	public interface IPpsSimpleOperation
	{
		void Undo();
		void Redo();

		bool CanUndo { get; }
		bool CanRedo { get; }
	} // interface IPpsSimpleOperation

	public interface IPpsCompoundOperation : IPpsSimpleOperation // ist eigentlich nur eine Klammer um Operationen
	{
		string Name { get; }
	} // interface IPpsCompoundOperation

	public interface IPpsCompoundOperationScope : IDisposable
	{
		void Commit();
		void Rollback();

		bool? IsCommitted { get; }
	}

	public interface IPpsUndoStack : IPpsSimpleOperation, IEnumerable<IPpsCompoundOperation>
	{
		IPpsCompoundOperationScope BeginOperation(string sName);
		/* 
		 * 
		 * Wahrscheinlich gibt es zwei Scope
		 *   MainScope -> der die IPpsSimpleOperation, wobei dieser von UndoStack implementiert werden kann?
		 *   SubScope -> der einen Marker für seine Operationen hat
		 *   
		 * Bsp: M   S   S
		 *  so1 |
		 *  so2 |
		 *  so3 |   |
		 *  so4 |   |   |
		 *  so5 |   |
		 *  so6 |
		 * 
		 * Was nicht passieren darf das sich die grenzen Überschneiden: also Commit/Rollback überkreuz
		 * 
		 */

		int Count { get; }
	}
}
