# Editor Improvements Backlog

Este documento lista as melhorias e correções necessárias para tornar o **ERus Editor** mais intuitivo e próximo aos padrões da indústria (como Unity e Unreal).

## 1. Sistema de Prefabs
- **Problema:** Não há como transformar uma entidade da cena em um "Prefab" reutilizável (salvar a entidade e seus componentes em um arquivo `.prefab` ou `.erus`).
- **Objetivo:** Permitir arrastar um objeto da *Hierarchy* para a janela *Project* para gerar um arquivo de Prefab.

## 2. Drag & Drop de Modelos 3D para a Cena
- **Problema:** É impossível instanciar um modelo 3D (ex: `.obj`, `.fbx`) na cena arrastando-o da janela *Project* para a *Scene View* ou *Hierarchy*.
- **Objetivo:** Implementar o evento de *Drag & Drop* (Payload ImGui) para que arrastar um arquivo 3D crie automaticamente uma entidade com `TransformComponent` e `MeshComponent`.

## 3. Renomear Entidades na Hierarchy
- **Problema:** Não é possível renomear entidades diretamente pela janela da *Hierarchy* (clique duplo ou atalho F2).
- **Objetivo:** Adicionar funcionalidade de edição de texto (InputText) in-line na árvore da Hierarchy para alterar o nome/tag da entidade.

## 4. Importação de Arquivos Externos (OS Drag & Drop)
- **Problema:** Não tem como importar arquivos de pastas aleatórias do Windows arrastando-os pelo Windows Explorer diretamente para dentro da janela *Project* da Engine.
- **Objetivo:** Capturar eventos de `Drop` do sistema operacional (via janela GLFW/Silk.NET) e copiar os arquivos soltos para o diretório de Assets ativo.

## 5. Movimentação de Arquivos (Project View)
- **Problema:** Atualmente, é possível arrastar um arquivo para *dentro* de uma subpasta, mas é impossível movê-lo para *fora* (para a raiz ou pasta pai).
- **Objetivo:** Adicionar uma área de "drop" para o diretório pai (ex: um botão `[ .. ]` ou a barra de navegação) para permitir mover arquivos de volta na hierarquia de pastas.

## 6. Abrir Arquivos no Editor Externo (VS Code, Bloco de Notas)
- **Problema:** Não é possível clicar duas vezes em um script C# ou arquivo de texto na *Project View* para abri-lo automaticamente no VS Code ou Bloco de Notas. É necessário ir até a pasta no Windows Explorer.
- **Objetivo:** Implementar um clique duplo (Double-Click) nos itens do Project Browser que dispare um processo do sistema operacional (`Process.Start`) para abrir o arquivo no programa padrão do usuário.
