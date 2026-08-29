namespace ERus.Engine.Graphics.Shaders;

/// <summary>
/// Código GLSL dos programas usados pelo <see cref="SceneRenderer"/>.
/// Separado do renderer para que a lógica de render não conviva com texto de shader.
/// </summary>
public static class ShaderSources
{
    /// <summary>Primitivas e sprites 2D: tiling/offset de UV, tint, alpha cutoff e resposta metallic/roughness.</summary>
    public const string PrimitiveVertex = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec2 aTexCoords;

        out vec2 TexCoords;
        out vec3 Normal;
        out vec3 FragPos;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform vec2 uTiling;
        uniform vec2 uOffset;

        void main()
        {
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
            TexCoords = (aTexCoords * uTiling) + uOffset;
            Normal = mat3(transpose(inverse(uModel))) * aNormal;
            FragPos = vec3(uModel * vec4(aPosition, 1.0));
        }";

    public const string PrimitiveFragment = @"
        #version 330 core
        in vec2 TexCoords;
        in vec3 Normal;
        in vec3 FragPos;

        out vec4 FragColor;

        uniform sampler2D uAlbedoTexture;
        uniform vec4 uColorTint;
        uniform int uHasTexture;
        uniform float uAlphaCutoff;
        uniform float uMetallic;
        uniform float uRoughness;
        uniform vec3 uViewPos;

        // Refletância base de um dielétrico; o metal usa o próprio albedo como cor de reflexo.
        const vec3 DielectricF0 = vec3(0.04);

        void main()
        {
            vec4 texColor = vec4(1.0);
            if (uHasTexture == 1)
            {
                texColor = texture(uAlbedoTexture, TexCoords);
            }

            vec4 finalColor = texColor * uColorTint;
            if (finalColor.a < uAlphaCutoff)
            {
                discard;
            }

            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(vec3(0.5, 1.0, 0.5));
            vec3 viewDir = normalize(uViewPos - FragPos);
            vec3 halfDir = normalize(lightDir + viewDir);

            float diff = max(dot(norm, lightDir), 0.35);

            // Metal não tem componente difusa própria: a energia migra para o especular.
            vec3 diffuse = finalColor.rgb * diff * (1.0 - uMetallic);

            // Rugosidade mapeada para expoente de Blinn-Phong (liso = lóbulo estreito).
            float alpha = max(uRoughness * uRoughness, 0.002);
            float shininess = 2.0 / (alpha * alpha) - 2.0;
            float specAmount = pow(max(dot(norm, halfDir), 0.0), shininess);

            // Superfície totalmente rugosa não gera destaque.
            float specMask = 1.0 - uRoughness;
            vec3 specColor = mix(DielectricF0, finalColor.rgb, uMetallic);
            vec3 specular = specColor * specAmount * specMask;

            FragColor = vec4(diffuse + specular, finalColor.a);
        }";

    /// <summary>Modelos importados via Assimp, com skinning por bones.</summary>
    public const string ModelVertex = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec2 aTexCoords;
        layout (location = 5) in ivec4 aBoneIds;
        layout (location = 6) in vec4 aWeights;

        out vec2 TexCoords;
        out vec3 Normal;
        out vec3 FragPos;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        const int MAX_BONES = 100;
        const int MAX_BONE_INFLUENCE = 4;
        uniform mat4 uFinalBonesMatrices[MAX_BONES];

        void main()
        {
            vec4 totalPosition = vec4(0.0f);
            vec3 totalNormal = vec3(0.0f);

            bool hasBones = false;
            for(int i = 0 ; i < MAX_BONE_INFLUENCE ; i++)
            {
                if(aBoneIds[i] == -1)
                    continue;

                if(aBoneIds[i] >= MAX_BONES)
                {
                    totalPosition = vec4(aPosition,1.0f);
                    break;
                }

                hasBones = true;
                vec4 localPosition = uFinalBonesMatrices[aBoneIds[i]] * vec4(aPosition, 1.0f);
                totalPosition += localPosition * aWeights[i];
                vec3 localNormal = mat3(uFinalBonesMatrices[aBoneIds[i]]) * aNormal;
                totalNormal += localNormal * aWeights[i];
            }

            if (!hasBones)
            {
                totalPosition = vec4(aPosition, 1.0f);
                totalNormal = aNormal;
            }

            gl_Position = uProjection * uView * uModel * totalPosition;
            TexCoords = aTexCoords;
            Normal = mat3(transpose(inverse(uModel))) * totalNormal;
            FragPos = vec3(uModel * totalPosition);
        }
        ";

    public const string ModelFragment = @"
        #version 330 core
        in vec2 TexCoords;
        in vec3 Normal;
        in vec3 FragPos;

        out vec4 FragColor;

        uniform sampler2D texture_diffuse1;
        uniform vec3 uColorTint;

        void main()
        {
            vec4 texColor = texture(texture_diffuse1, TexCoords);
            if (texColor.a < 0.1) texColor = vec4(0.8, 0.8, 0.8, 1.0);

            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(vec3(0.5, 1.0, 0.5));

            float diff = max(dot(norm, lightDir), 0.3);
            vec3 diffuse = diff * texColor.rgb;

            FragColor = vec4(diffuse * uColorTint, texColor.a);
        }
        ";

    /// <summary>Linhas com cor por vértice: grid do editor e wireframe de câmera.</summary>
    public const string LineVertex = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;

        out vec3 vertexColor;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
            vertexColor = aColor;
        }";

    public const string LineFragment = @"
        #version 330 core
        in vec3 vertexColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vec4(vertexColor, 1.0);
        }";
}
