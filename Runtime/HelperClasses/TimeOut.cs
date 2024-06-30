using System;
using System.Collections;

namespace MHZ.HelperClasses
{
    public class TimeOut
    {
        public float Time { private set; get; }
        
        public bool IsTimerRunning { private set; get; }

        public TimeOut(float time) => Time = time;

        public IEnumerator StartTimer(Action onComplete = null)
        {
            if(IsTimerRunning) yield break;
            
            IsTimerRunning = true;
            var time = Time;
            while(time > 0)
            {
                time -= UnityEngine.Time.deltaTime;
                yield return null;
            }

            IsTimerRunning = false;
            
            onComplete?.Invoke();
        }

        public bool IsTimeFinished() => Time <= 0;

        public void Reset() => IsTimerRunning = false;
    }
}
