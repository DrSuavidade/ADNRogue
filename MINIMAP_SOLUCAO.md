# 🗺️ **Solução para o Problema do Minimap**

## 📋 **Problema Identificado**

Os ícones do minimap não aparecem no Unity porque **os prefabs das salas não têm os sprites configurados no campo `minimapIcon`**.

### Estrutura Atual:
- ✅ **Código está correto**: `RoomInstance.cs` tem o campo `minimapIcon`
- ✅ **Sprites existem**: Estão em `Assets/Resources/Prefabs/MiniMap/Icones/`
- ❌ **Configuração falta**: Os prefabs não têm os sprites atribuídos

## 🛠️ **Como Resolver**

### **Opção 1: Usar o Script Automático (RECOMENDADO)**

Criei um script Editor que faz tudo automaticamente:

1. **No Unity**, vá ao menu superior: `Tools > Verify Minimap Sprite Import Settings`
   - Isso garante que os sprites estão importados como "Sprite (2D and UI)"

2. Depois, vá a: `Tools > Assign Minimap Icons to Room Prefabs`
   - Isso atribui automaticamente cada sprite ao prefab correspondente

3. **Pronto!** Os ícones devem aparecer quando dar Play.

---

### **Opção 2: Configurar Manualmente**

Se preferir fazer manualmente:

1. **Configurar os Sprites:**
   - Vá para `Assets/Resources/Prefabs/MiniMap/Icones/`
   - Selecione cada imagem (RoomB1.png, RoomB2.png, etc.)
   - No Inspector, certifique-se que:
     - **Texture Type**: `Sprite (2D and UI)`
     - **Sprite Mode**: `Single`
     - Clique em **Apply**

2. **Configurar os Prefabs:**
   - Vá para `Assets/Resources/WorldGenAssets/Prefabs/RoomS/`
   - Selecione cada prefab (RoomB1, RoomB2, etc.)
   - No Inspector, encontre o componente **RoomInstance**
   - Arraste o sprite correspondente para o campo **Minimap Icon**
     - Exemplo: Para `RoomB1.prefab` → arraste `RoomB1.png`

3. **Salvar:**
   - Clique em **Apply** em cada prefab
   - Ou use Ctrl+S para salvar

---

## 🔍 **Verificar se Funcionou**

1. Entre no Play Mode
2. Os ícones das salas devem aparecer no minimap
3. Se ainda não aparecer, verifique o Console para mensagens de debug

### Debug Logs Esperados:
```
[MinimapRoomIcon] Applied room sprite: RoomB1
[MinimapRoomIcon] Applied room sprite: RoomSmall
```

### Se aparecer isto (problema):
```
[MinimapRoomIcon] Room RoomB1 has NO MinimapIcon! Using fallback type icon.
```
Significa que o sprite não está atribuído ao prefab.

---

## 📝 **Notas Importantes**

- **Você não precisa usar os campos `iconHub`, `iconCombat`, `iconShop` no MinimapUI** - Esses são apenas fallback (backup) caso alguma sala não tenha sprite configurado
- **Cada sala usa seu próprio sprite personalizado** através do campo `minimapIcon` no componente `RoomInstance`
- **O BaseHUD** também precisa ter um sprite se for o hub principal

---

## 🎯 **Próximos Passos**

Depois de configurar, se quiser melhorar o minimap:

1. **Ajustar o tamanho dos ícones**: 
   - Modifique `spacing` no `MinimapUI` (atualmente 100f)

2. **Criar um ícone especial para o hub**:
   - Crie uma imagem `BaseHUD.png` na pasta de ícones
   - O script automático vai atribuí-la ao BaseHUD

3. **Cores diferentes para salas visitadas/não visitadas**:
   - Ajuste em `MinimapRoomIcon` → `colorUnvisited`, `colorVisited`, `colorActive`
