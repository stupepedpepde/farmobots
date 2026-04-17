using System;
using System.Collections.Generic;
using System.Linq;
using Eflatun.SceneReference;

namespace Game.Scripts.Core.Scene {
    public enum SceneType {
        ACTIVE,
        MENU,
        INTERFACE,
        HUD,
        CINEMATIC,
        ENVIRONMENT,
        TOOLING
    }
    
    [Serializable]
    public class SceneData {
        public SceneReference reference;
        public string name => reference.Name;
        public SceneType type;
    }

    [Serializable]
    public class SceneGroup {
        public string groupName = "New Scene Group";
        public List<SceneData> scenes;

        public string FindSceneNameByType(SceneType type) { return scenes.FirstOrDefault(scene => scene.type == type)?.reference.Name; }
    }
}