using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;

namespace Fluff_Toolbox.Extensions.UnityExtensions {

	//CREDITS TO https://gist.github.com/benblo/10732554

	public class FEditorCoroutine
	{
		public static FEditorCoroutine Start( IEnumerator _routine )
		{
			FEditorCoroutine coroutine = new FEditorCoroutine(_routine);
			coroutine.Start();
			return coroutine;
		}

		readonly IEnumerator routine;
		FEditorCoroutine( IEnumerator _routine )
		{
			routine = _routine;
		}

		public void Start()
		{
			EditorApplication.update += Update;
		}
		public void Stop()
		{
			EditorApplication.update -= Update;
		}

		void Update()
		{

			if (!routine.MoveNext())
			{
				Stop();
			}
		}
	}
}
