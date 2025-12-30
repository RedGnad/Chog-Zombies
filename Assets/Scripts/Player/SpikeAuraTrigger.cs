using System.Collections.Generic;
using UnityEngine;
using ChogZombies.Enemies;
using ChogZombies.Loot;

namespace ChogZombies.Player
{
    /// <summary>
    /// Ancien composant expérimental pour gérer l'aura via un collider séparé.
    /// Il est désormais neutralisé et ne fait plus rien : la logique de dégâts
    /// de l'aura est gérée côté ennemis en fonction de la distance au joueur.
    /// </summary>
    public class SpikeAuraTrigger : MonoBehaviour
    {
        void Awake()
        {
            // Composant neutralisé : aucune configuration de collider/rigidbody.
        }

        void Update()
        {
            // Composant neutralisé : aucun comportement par frame.
        }

        void OnTriggerStay(Collider other)
        {
            // Composant neutralisé : ne réagit plus aux triggers.
        }

        void OnTriggerExit(Collider other)
        {
            // Composant neutralisé : rien à faire.
        }
    }
}
